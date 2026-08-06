namespace LibraryManager.Models;

/// <summary>
/// Excepción segura para fallos de almacenamiento. Los detalles técnicos permanecen
/// en los registros mediante InnerException y la interfaz muestra un mensaje amigable.
/// </summary>
public sealed class LibraryDataException : Exception
{
    public LibraryDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
