namespace FsEqual.Tool.Core;

internal enum DifferenceType
{
    MissingLeft,
    MissingRight,
    TypeMismatch,
    SizeMismatch,
    HashMismatch,
    MetadataMismatch,
}
