using LibraryManager.Models;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Services;

public class BookRepository
{
    private const string BooksCollectionName = "books";
    private const string LoansCollectionName = "loans";
    private readonly string _databasePath;
    private readonly ILogger<BookRepository> _logger;
    private readonly object _initializationLock = new();
    private bool _isInitialized;

    public BookRepository(ILogger<BookRepository> logger)
    {
        _logger = logger;
        _databasePath = Path.Combine(FileSystem.AppDataDirectory, "library-manager.db");
    }

    public IReadOnlyList<Book> GetAll(string? searchTerm = null) =>
        Execute("load the catalog", database =>
        {
            var books = database.GetCollection<Book>(BooksCollectionName);

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return books.Query()
                    .Where(book => book.Status != BookStatus.Lost)
                    .OrderBy(book => book.Title)
                    .ToList();
            }

            var term = searchTerm.Trim();
            return books.Query()
                .Where(book => book.Status != BookStatus.Lost)
                .Where(book => book.Title.Contains(term) || book.Author.Contains(term))
                .OrderBy(book => book.Title)
                .ToList();
        });

    public void Add(Book book) =>
        Execute("add the book", database =>
            database.GetCollection<Book>(BooksCollectionName).Insert(book));

    public void Delete(ObjectId id) =>
        Execute("delete the book", database =>
        {
            var books = database.GetCollection<Book>(BooksCollectionName);
            var book = books.FindById(id);

            if (book is null)
            {
                return;
            }

            if (book.Status != BookStatus.Available)
            {
                throw new InvalidOperationException(
                    "Only available books can be permanently deleted. Mark a checked-out book as lost instead.");
            }

            books.Delete(id);
        });

    public LoanRecord? GetActiveLoan(ObjectId bookId) =>
        Execute("load the active loan", database =>
            database.GetCollection<LoanRecord>(LoansCollectionName)
                .FindOne(loan =>
                    loan.BookId == bookId &&
                    loan.ActualReturnDate == null &&
                    loan.MarkedLostOn == null));

    public IReadOnlyList<LoanRecord> GetLoanHistory(ObjectId bookId) =>
        Execute("load the loan history", database =>
            database.GetCollection<LoanRecord>(LoansCollectionName)
                .Find(loan => loan.BookId == bookId)
                .OrderByDescending(loan => loan.LentOn)
                .ToList());

    public IReadOnlyList<BookStatusReportItem> GetStatusReport() =>
        Execute("load the status report", database =>
        {
            var books = database.GetCollection<Book>(BooksCollectionName)
                .Query()
                .Where(book => book.Status != BookStatus.Lost)
                .OrderBy(book => book.Title)
                .ToList();
            var activeLoans = database.GetCollection<LoanRecord>(LoansCollectionName)
                .Find(loan =>
                    loan.ActualReturnDate == null &&
                    loan.MarkedLostOn == null)
                .GroupBy(loan => loan.BookId)
                .ToDictionary(group => group.Key, group => group.First());

            return books.Select(book =>
            {
                activeLoans.TryGetValue(book.Id, out var activeLoan);
                return new BookStatusReportItem
                {
                    BookId = book.Id,
                    Title = book.Title,
                    Author = book.Author,
                    Isbn = book.Isbn,
                    PublicationYear = book.PublicationYear,
                    Status = book.Status,
                    BorrowerFullName = activeLoan?.BorrowerFullName ?? string.Empty,
                    BorrowerId = activeLoan?.BorrowerId ?? string.Empty,
                    ExpectedReturnDate = activeLoan?.ExpectedReturnDate
                };
            }).ToList();
        });

    public IReadOnlyList<BookStatusReportItem> GetLostBooksReport() =>
        Execute("load the lost-books report", database =>
        {
            var books = database.GetCollection<Book>(BooksCollectionName)
                .Query()
                .Where(book => book.Status == BookStatus.Lost)
                .OrderBy(book => book.Title)
                .ToList();
            var lostLoans = database.GetCollection<LoanRecord>(LoansCollectionName)
                .Find(loan => loan.MarkedLostOn != null)
                .GroupBy(loan => loan.BookId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(loan => loan.MarkedLostOn).First());

            return books.Select(book =>
            {
                lostLoans.TryGetValue(book.Id, out var lostLoan);
                return new BookStatusReportItem
                {
                    BookId = book.Id,
                    Title = book.Title,
                    Author = book.Author,
                    Isbn = book.Isbn,
                    PublicationYear = book.PublicationYear,
                    Status = book.Status,
                    BorrowerFullName = lostLoan?.BorrowerFullName ?? string.Empty,
                    BorrowerId = lostLoan?.BorrowerId ?? string.Empty,
                    ExpectedReturnDate = lostLoan?.ExpectedReturnDate ?? book.ExpectedReturnDate,
                    LostOn = lostLoan?.MarkedLostOn ?? book.LostOn
                };
            }).ToList();
        });

    public LoanRecord LendBook(
        ObjectId bookId,
        string borrowerFullName,
        string borrowerId,
        DateTime expectedReturnDate)
    {
        if (!BorrowerInputRules.IsValidFullName(borrowerFullName))
        {
            throw new ArgumentException(
                "Enter at least two names using letters and spaces only.",
                nameof(borrowerFullName));
        }

        if (!BorrowerInputRules.IsValidId(borrowerId))
        {
            throw new ArgumentException(
                "The borrower ID can contain only numbers.",
                nameof(borrowerId));
        }

        return Execute("record the loan", database =>
        {
            var books = database.GetCollection<Book>(BooksCollectionName);
            var loans = database.GetCollection<LoanRecord>(LoansCollectionName);
            var book = books.FindById(bookId)
                ?? throw new InvalidOperationException("The selected book no longer exists.");

            if (book.Status != BookStatus.Available ||
                loans.Exists(loan =>
                    loan.BookId == bookId &&
                    loan.ActualReturnDate == null &&
                    loan.MarkedLostOn == null))
            {
                throw new InvalidOperationException("This book is already checked out.");
            }

            var loan = new LoanRecord
            {
                BookId = book.Id,
                BookTitle = book.Title,
                BookIsbn = book.Isbn,
                BorrowerFullName = BorrowerInputRules.NormalizeFullName(borrowerFullName),
                BorrowerId = borrowerId.Trim(),
                LentOn = DateTime.Now,
                ExpectedReturnDate = expectedReturnDate.Date
            };

            book.Status = BookStatus.CheckedOut;
            book.ExpectedReturnDate = loan.ExpectedReturnDate;

            database.BeginTrans();
            try
            {
                loans.Insert(loan);
                if (!books.Update(book))
                {
                    throw new InvalidOperationException("The book status could not be updated.");
                }

                database.Commit();
                return loan;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        });
    }

    public LoanRecord ReturnBook(ObjectId bookId) =>
        Execute("record the return", database =>
        {
            var books = database.GetCollection<Book>(BooksCollectionName);
            var loans = database.GetCollection<LoanRecord>(LoansCollectionName);
            var book = books.FindById(bookId)
                ?? throw new InvalidOperationException("The selected book no longer exists.");
            var loan = loans.FindOne(item =>
                item.BookId == bookId &&
                item.ActualReturnDate == null &&
                item.MarkedLostOn == null);

            if (book.Status != BookStatus.CheckedOut || loan is null)
            {
                throw new InvalidOperationException("This book does not have an active loan.");
            }

            loan.ActualReturnDate = DateTime.Now;
            book.Status = BookStatus.Available;
            book.ExpectedReturnDate = null;

            database.BeginTrans();
            try
            {
                if (!loans.Update(loan) || !books.Update(book))
                {
                    throw new InvalidOperationException("The return could not be recorded.");
                }

                database.Commit();
                return loan;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        });

    public LoanRecord MarkBookLost(ObjectId bookId) =>
        Execute("mark the book as lost", database =>
        {
            var books = database.GetCollection<Book>(BooksCollectionName);
            var loans = database.GetCollection<LoanRecord>(LoansCollectionName);
            var book = books.FindById(bookId)
                ?? throw new InvalidOperationException("The selected book no longer exists.");
            var loan = loans.FindOne(item =>
                item.BookId == bookId &&
                item.ActualReturnDate == null &&
                item.MarkedLostOn == null);

            if (book.Status != BookStatus.CheckedOut || loan is null)
            {
                throw new InvalidOperationException(
                    "Only a currently checked-out book can be marked as lost.");
            }

            var markedLostOn = DateTime.Now;
            loan.MarkedLostOn = markedLostOn;
            book.Status = BookStatus.Lost;
            book.LostOn = markedLostOn;

            database.BeginTrans();
            try
            {
                if (!loans.Update(loan) || !books.Update(book))
                {
                    throw new InvalidOperationException(
                        "The lost-book record could not be completed.");
                }

                database.Commit();
                return loan;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        });

    private T Execute<T>(string operation, Func<LiteDatabase, T> action)
    {
        try
        {
            using var database = OpenDatabase();
            EnsureInitialized(database);
            return action(database);
        }
        catch (LiteException exception)
            when (exception.ErrorCode == LiteException.INDEX_DUPLICATE_KEY)
        {
            throw;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            _logger.LogError(exception, "Could not {Operation} using {DatabasePath}.", operation, _databasePath);
            throw new LibraryDataException(
                $"The library could not {operation}. Please check that the app can access its local data and try again.",
                exception);
        }
    }

    private void Execute(string operation, Action<LiteDatabase> action) =>
        Execute(operation, database =>
        {
            action(database);
            return true;
        });

    private void EnsureInitialized(LiteDatabase database)
    {
        if (_isInitialized)
        {
            return;
        }

        lock (_initializationLock)
        {
            if (_isInitialized)
            {
                return;
            }

            var books = database.GetCollection<Book>(BooksCollectionName);
            books.EnsureIndex(book => book.Isbn, unique: true);
            books.EnsureIndex(book => book.Status);

            var loans = database.GetCollection<LoanRecord>(LoansCollectionName);
            loans.EnsureIndex(loan => loan.BookId);
            loans.EnsureIndex(loan => loan.ActualReturnDate);
            loans.EnsureIndex(loan => loan.MarkedLostOn);
            _isInitialized = true;
        }
    }

    private static bool IsStorageFailure(Exception exception) =>
        exception is LiteException or IOException or UnauthorizedAccessException;

    private LiteDatabase OpenDatabase() => new(_databasePath);
}
