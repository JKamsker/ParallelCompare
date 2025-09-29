namespace FsEqual.Core;

public enum DifferenceType
{
    MissingLeft,
    MissingRight,
    TypeMismatch,
    SizeMismatch,
    HashMismatch,
    TimestampMismatch,
    Error
}
