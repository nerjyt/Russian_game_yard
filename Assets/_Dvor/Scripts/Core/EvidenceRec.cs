using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Вещдок в сетевом списке. Знает ЧТО произошло и ГДЕ, но не знает КТО —
    /// имя виновника живёт только на сервере. Поэтому улику можно подбросить.
    /// </summary>
    public struct EvidenceRec : INetworkSerializable, IEquatable<EvidenceRec>
    {
        public int Id;
        public FixedString32Bytes DeedId;
        public FixedString32Bytes Label;
        public Vector3 Pos;
        public ulong Holder;          // ulong.MaxValue = лежит на земле

        public bool OnGround => Holder == ulong.MaxValue;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref DeedId);
            s.SerializeValue(ref Label);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Holder);
        }

        public bool Equals(EvidenceRec o) => Id == o.Id;
    }
}
