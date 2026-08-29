using System;

namespace XeptKit.Asset
{
    /// <summary>
    /// 资产加载失败异常（fail-fast）。
    /// 包装底层实现异常（如 Addressables 的 InvalidKeyException），不泄漏底层类型到接口层。
    /// </summary>
    public sealed class AssetLoadException : Exception
    {
        /// <summary>加载失败的资产地址。</summary>
        public string Address { get; }

        /// <summary>请求的资产类型。</summary>
        public Type RequestedType { get; }

        public AssetLoadException(string address, Type requestedType, Exception innerException)
            : base($"加载资产 '{address}' 失败（请求类型 {requestedType.Name}），详见 InnerException", innerException)
        {
            Address = address;
            RequestedType = requestedType;
        }
    }
}
