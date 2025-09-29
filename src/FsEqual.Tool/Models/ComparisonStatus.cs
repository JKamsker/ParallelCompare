namespace FsEqual.Tool.Models;

public enum ComparisonStatus
{
    Equal,
    MissingLeft,
    MissingRight,
    TypeMismatch,
    SizeMismatch,
    HashMismatch,
    MetadataMismatch,
    Error,
}
