using System;
using Unity.Netcode;

namespace MakeTrap.Multiplayer
{
    public enum PlayerTeam : byte { Kids, Thieves }

    public struct PlayerSessionData : INetworkSerializable, IEquatable<PlayerSessionData>
    {
        public ulong ClientId;
        public int Slot;
        public bool Ready;
        public bool IsBot;
        public int CharacterIndex;
        public PlayerTeam Team => Slot < 2 ? PlayerTeam.Kids : PlayerTeam.Thieves;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref Ready);
            serializer.SerializeValue(ref IsBot);
            serializer.SerializeValue(ref CharacterIndex);
        }
        public bool Equals(PlayerSessionData other) => ClientId == other.ClientId && Slot == other.Slot && Ready == other.Ready && IsBot == other.IsBot && CharacterIndex == other.CharacterIndex;
    }
}
