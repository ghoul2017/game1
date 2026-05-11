using System;
using System.Security.Cryptography;
using System.Text;
using Shared.Core.Log;

namespace Shared.Core;

/// <summary>
/// Token 签名器。使用 HMAC-SHA256 对 userId:roleId:timestamp 签名，
/// 生成无状态 Token 替代 TokenStore。
/// Token 格式：Base64(payload):Base64(signature)
/// </summary>
public class TokenSigner
{
    private static readonly ILog Log = LogManager.GetLogger<TokenSigner>();

    public static TokenSigner Instance { get; } = new();

    private readonly byte[] _key;

    private TokenSigner()
    {
        _key = RandomNumberGenerator.GetBytes(32);
        Log.Info("TokenSigner 初始化");
    }

    /// <summary>
    /// 生成签名 Token。
    /// </summary>
    public string Generate(long userId, long roleId = 0)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{userId}:{roleId}:{timestamp}";
        var signature = ComputeHmac(payload);
        return $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))}:{Convert.ToBase64String(signature)}";
    }

    /// <summary>
    /// 验证 Token 并解析。返回 (userId, roleId, timestamp)，无效或过期返回 null。
    /// </summary>
    public (long userId, long roleId, long timestamp)? Validate(string token, int expireMinutes = 5)
    {
        try
        {
            var sepIndex = token.IndexOf(':');
            if (sepIndex < 0) return null;

            var payloadBytes = Convert.FromBase64String(token.Substring(0, sepIndex));
            var signatureBytes = Convert.FromBase64String(token.Substring(sepIndex + 1));

            var payload = Encoding.UTF8.GetString(payloadBytes);

            // 验证签名
            var expected = ComputeHmac(payload);
            if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expected))
                return null;

            // 解析 payload
            var parts = payload.Split(':');
            if (parts.Length != 3) return null;
            if (!long.TryParse(parts[0], out var userId)) return null;
            if (!long.TryParse(parts[1], out var roleId)) return null;
            if (!long.TryParse(parts[2], out var timestamp)) return null;

            // 检查过期
            var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            if (DateTimeOffset.UtcNow - tokenTime > TimeSpan.FromMinutes(expireMinutes))
                return null;

            return (userId, roleId, timestamp);
        }
        catch (Exception e)
        {
            Log.Warn($"Token 解析失败: {e.Message}");
            return null;
        }
    }

    private byte[] ComputeHmac(string payload)
    {
        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }
}
