namespace NL.Fork.Core;

/// <summary>
/// Server-side state / packet validation before NL evaluation (Phase P anti-cheat layer).
/// Rejects impossible transitions locally so they never commit.
/// </summary>
public sealed class ForkStateValidator
{
    public double MaxMovePerTick { get; init; } = 50;

    public double MaxShootDamage { get; init; } = 200;

    public ForkValidationResult ValidateShoot(ForkPlayerState shooter, ForkPlayerState? target, double damage)
    {
        if (!shooter.Connected)
        {
            return ForkValidationResult.Reject("shooter disconnected");
        }

        if (!shooter.Alive)
        {
            return ForkValidationResult.Reject("shooter dead");
        }

        if (!shooter.HasWeapon)
        {
            return ForkValidationResult.Reject("no weapon");
        }

        if (damage <= 0 || damage > MaxShootDamage)
        {
            return ForkValidationResult.Reject("invalid damage");
        }

        if (target is not null && !target.Alive)
        {
            return ForkValidationResult.Reject("target dead");
        }

        return ForkValidationResult.Ok();
    }

    public ForkValidationResult ValidateMove(ForkPlayerState player, double x, double y, double z, ForkWorldState world)
    {
        if (!player.Connected)
        {
            return ForkValidationResult.Reject("disconnected");
        }

        if (!player.Alive)
        {
            return ForkValidationResult.Reject("dead");
        }

        var dx = x - player.X;
        var dy = y - player.Y;
        var dz = z - player.Z;
        var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (dist > MaxMovePerTick)
        {
            return ForkValidationResult.Reject($"teleport ({dist:F1} > {MaxMovePerTick})");
        }

        if (x < world.BoundaryMin || x > world.BoundaryMax
            || y < world.BoundaryMin || y > world.BoundaryMax
            || z < world.BoundaryMin || z > world.BoundaryMax)
        {
            return ForkValidationResult.Reject("out of bounds");
        }

        return ForkValidationResult.Ok();
    }

    public ForkValidationResult ValidateRespawn(ForkPlayerState player, double requestedHealth)
    {
        if (!player.Connected)
        {
            return ForkValidationResult.Reject("disconnected");
        }

        if (player.Alive && requestedHealth > 0)
        {
            return ForkValidationResult.Reject("already alive");
        }

        return ForkValidationResult.Ok();
    }
}

public sealed record ForkValidationResult(bool Allowed, string? Reason)
{
    public static ForkValidationResult Ok() => new(true, null);

    public static ForkValidationResult Reject(string reason) => new(false, reason);
}
