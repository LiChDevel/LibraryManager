using LiteDB;

namespace LibraryManager.Models;

/// <summary>
/// Registro persistente del catálogo. Un libro perdido permanece almacenado
/// para conservar su historial, aunque se oculte del catálogo activo.
/// </summary>
public class Book
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public int PublicationYear { get; set; }
    public BookStatus Status { get; set; } = BookStatus.Available;
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? LostOn { get; set; }
    public string StatusLabel => Status switch
    {
        BookStatus.Available => "Available",
        BookStatus.CheckedOut => "Checked out",
        BookStatus.Lost => "Lost",
        _ => "Unknown"
    };
    public string AccessibilityDescription =>
        $"{Title}, by {Author}, published {PublicationYear}, ISBN {Isbn}, status {StatusLabel}.";
    public string AutomationIdValue => $"Book_{Id}";
}

// Mantener estables los valores numéricos porque LiteDB guarda este enum como entero.
public enum BookStatus { Available, CheckedOut, Lost }
