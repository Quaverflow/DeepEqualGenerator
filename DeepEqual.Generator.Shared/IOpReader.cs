namespace DeepEqual.Generator.Shared;

public interface IOpReader
{
    bool TryRead(out DeltaOp op);
}