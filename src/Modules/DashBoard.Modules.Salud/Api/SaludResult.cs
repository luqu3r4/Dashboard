namespace DashBoard.Modules.Salud.Api;

public enum SaludStatus { Ok, NotConfigured, Unreachable, Unauthorized, InvalidResponse }

public sealed record SaludResult<T>(SaludStatus Status, T? Data)
{
    public static SaludResult<T> Ok(T data) => new(SaludStatus.Ok, data);

    public static SaludResult<T> Fail(SaludStatus status) => new(status, default);
}
