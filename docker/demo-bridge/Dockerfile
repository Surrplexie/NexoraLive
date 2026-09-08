FROM python:3.12-slim

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && pip install --no-install-recommends --no-cache-dir websocket-client==1.8.0

COPY integrations/python/nl_bridge.py /app/nl_bridge.py
COPY docker/demo-bridge/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENV NL_BRIDGE_URL=""
ENV NL_ADMIT_URL=""
ENV NL_BRIDGE_INTERVAL=8
ENV NL_BRIDGE_RECONNECT_DELAY=3
ENV NL_BRIDGE_WAIT_URL=http://session-server:27020/api/v1/demo/status

ENTRYPOINT ["/entrypoint.sh"]
