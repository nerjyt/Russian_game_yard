using Unity.Netcode.Components;

namespace Dvor
{
    /// <summary>
    /// Движение считает владелец — для прототипа это честный размен:
    /// телепорт читом возможен, но всё, что решает исход партии, сервер проверяет сам.
    /// </summary>
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
