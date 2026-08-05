namespace LibraryManager.Models;

public sealed class LibraryDataException : Exception
{
    public LibraryDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
