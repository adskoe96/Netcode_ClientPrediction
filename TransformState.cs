using Unity.Netcode;
using UnityEngine;

internal class TransformState : INetworkSerializable
{
    internal int Tick;
    internal Vector3 Position;
    internal Quaternion Rotation;
    internal bool HasStartedMoving;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out Tick);
            reader.ReadValueSafe(out Position);
            reader.ReadValueSafe(out Rotation);
            reader.ReadValueSafe(out HasStartedMoving);
        }
        else
        {
            var writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(Tick);
            writer.WriteValueSafe(Position);
            writer.WriteValueSafe(Rotation);
            writer.WriteValueSafe(HasStartedMoving);
        }
    }
}