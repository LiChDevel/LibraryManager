using LiteDB;

namespace LibraryManager.Models;

public sealed class BookStatusReportItem
{
    public ObjectId BookId { get; init; } = ObjectId.Empty;
    public string Title { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Isbn { get; init; } = string.Empty;
    public int PublicationYear { get; init; }
    public BookStatus Status { get; init; }
    public string BorrowerFullName { get; init; } = string.Empty;
    public string BorrowerId { get; init; } = string.Empty;
    public DateTime? ExpectedReturnDate { get; init; }
    public DateTime? LostOn { get; init; }

    public string CatalogDisplay => $"{Author} · {PublicationYear} · ISBN {Isbn}";
    public string LoanDisplay => Status == BookStatus.Available
        ? "Ready to lend"
        : $"{BorrowerFullName} · ID {BorrowerId}";
    public string ExpectedReturnDisplay => ExpectedReturnDate is null
        ? string.Empty
        : $"Expected return {ExpectedReturnDate.Value:MMMM d, yyyy}";
    public string LostOnDisplay => LostOn is null
        ? string.Empty
        : $"Marked lost {LostOn.Value:MMMM d, yyyy h:mm tt}";
}
