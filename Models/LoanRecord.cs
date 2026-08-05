using LiteDB;

namespace LibraryManager.Models;

public class LoanRecord
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public ObjectId BookId { get; set; } = ObjectId.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string BookIsbn { get; set; } = string.Empty;
    public string BorrowerFullName { get; set; } = string.Empty;
    public string BorrowerId { get; set; } = string.Empty;
    public DateTime LentOn { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public DateTime? MarkedLostOn { get; set; }

    public string BorrowerDisplay => $"{BorrowerFullName} · ID {BorrowerId}";
    public string LentOnDisplay => $"Lent {LentOn:MMMM d, yyyy h:mm tt}";
    public string ExpectedReturnDisplay =>
        $"Expected return {ExpectedReturnDate:MMMM d, yyyy}";
    public string ActualReturnDisplay => MarkedLostOn is not null
        ? $"Marked lost {MarkedLostOn.Value:MMMM d, yyyy h:mm tt}"
        : ActualReturnDate is not null
            ? $"Returned {ActualReturnDate.Value:MMMM d, yyyy h:mm tt}"
            : "Still checked out";
    public string AccessibilityDescription =>
        $"{BorrowerDisplay}. {LentOnDisplay}. {ExpectedReturnDisplay}. {ActualReturnDisplay}.";
    public string AutomationIdValue => $"Loan_{Id}";
}
