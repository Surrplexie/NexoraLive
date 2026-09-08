//! NL Game Integration Spec v1 — reference bridge (Rust / WebSocket).
//!
//! ```bash
//! cargo run -- --url "ws://127.0.0.1:27021/nl/v1?token=TOKEN" --sample
//! ```

use clap::Parser;
use futures_util::{SinkExt, StreamExt};
use serde_json::{json, Value};
use std::time::{SystemTime, UNIX_EPOCH};
use tokio_tungstenite::{connect_async, tungstenite::Message};

#[derive(Parser)]
#[command(name = "nl_bridge")]
struct Args {
    #[arg(long, default_value = "ws://127.0.0.1:27021/nl/v1")]
    url: String,
    #[arg(long)]
    sample: bool,
}

fn now_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis() as u64
}

fn emit(event: &str, player: &str, props: Option<Value>) -> String {
    let mut line = json!({
        "nl": 1,
        "event": event,
        "player": player,
        "ts": now_ms(),
    });
    if let Some(p) = props {
        line["props"] = p;
    }
    line.to_string()
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args = Args::parse();
    let (ws, _) = connect_async(&args.url).await?;
    println!("[nl bridge] connected {}", args.url);
    let (mut write, mut read) = ws.split();

    if args.sample {
        for (event, player, props) in [
            ("sessionStart", "Alice", Some(json!({"map.id": 1}))),
            ("playerJoin", "Alice", Some(json!({"player.alive": 1}))),
            ("shoot", "Alice", Some(json!({"weapon.damage": 12}))),
            ("shoot", "Bob", Some(json!({"weapon.damage": 50}))),
        ] {
            write
                .send(Message::Text(emit(event, player, props)))
                .await?;
        }
        tokio::time::sleep(std::time::Duration::from_millis(500)).await;
        return Ok(());
    }

    tokio::spawn(async move {
        while let Some(msg) = read.next().await {
            match msg {
                Ok(Message::Text(text)) => {
                    for line in text.lines() {
                        let trimmed = line.trim();
                        if !trimmed.is_empty() {
                            println!("[nl action] {}", trimmed);
                        }
                    }
                }
                Ok(Message::Close(_)) | Err(_) => break,
                _ => {}
            }
        }
    });

    let stdin = tokio::io::stdin();
    let mut reader = tokio::io::BufReader::new(stdin);
    let mut line = String::new();
    loop {
        line.clear();
        if tokio::io::AsyncBufReadExt::read_line(&mut reader, &mut line)
            .await
            .unwrap_or(0)
            == 0
        {
            break;
        }
        let trimmed = line.trim();
        if !trimmed.is_empty() {
            write.send(Message::Text(trimmed.to_string())).await?;
        }
    }

    Ok(())
}
