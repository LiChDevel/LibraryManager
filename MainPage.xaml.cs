using System.Collections.ObjectModel;
using LibraryManager.Models;
using LibraryManager.Services;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace LibraryManager;

public partial class MainPage : ContentPage
{
    private readonly BookRepository _repository;
    private readonly ILogger<MainPage> _logger;
    private readonly ObservableCollection<Book> _books = [];
    private readonly ObservableCollection<LoanRecord> _loanHistory = [];
    private readonly ObservableCollection<BookStatusReportItem> _availableBooks = [];
    private readonly ObservableCollection<BookStatusReportItem> _checkedOutBooks = [];
    private readonly ObservableCollection<BookStatusReportItem> _lostBooks = [];
    private Book? _selectedBook;
    private bool _isFilteringBorrowerInput;
    private bool _isShowingOperationError;

    public MainPage(BookRepository repository, ILogger<MainPage> logger)
    {
        InitializeComponent();
        _repository = repository;
        _logger = logger;
        BooksCollection.ItemsSource = _books;
        BindableLayout.SetItemsSource(LoanHistoryList, _loanHistory);
        AvailableBooksReportCollection.ItemsSource = _availableBooks;
        CheckedOutBooksReportCollection.ItemsSource = _checkedOutBooks;
        LostBooksReportCollection.ItemsSource = _lostBooks;
        ExpectedReturnDatePicker.MinimumDate = DateTime.Today;
        ExpectedReturnDatePicker.Date = DateTime.Today.AddDays(14);
        LoadBooks();
    }

    private void LoadBooks()
    {
        try
        {
            var selectedId = _selectedBook?.Id;
            _books.Clear();

            foreach (var book in _repository.GetAll(SearchEntry?.Text))
            {
                _books.Add(book);
            }

            _selectedBook = selectedId is null
                ? null
                : _books.FirstOrDefault(book => book.Id == selectedId);
            BooksCollection.SelectedItem = _selectedBook;
            ShowSelectedBook();
        }
        catch (Exception exception)
        {
            _books.Clear();
            _selectedBook = null;
            QueueOperationError("load the catalog", exception);
        }
    }

    private async void OnSaveBookClicked(object sender, EventArgs e)
    {
        if (!int.TryParse(YearEntry.Text, out var year) ||
            year < 1 ||
            year > DateTime.Today.Year)
        {
            await DisplayAlert(
                "Check the publication year",
                $"Enter a year between 1 and {DateTime.Today.Year}.",
                "OK");
            return;
        }

        var book = new Book
        {
            Title = TitleEntry.Text?.Trim() ?? string.Empty,
            Author = AuthorEntry.Text?.Trim() ?? string.Empty,
            Isbn = IsbnEntry.Text?.Trim() ?? string.Empty,
            PublicationYear = year
        };

        if (string.IsNullOrWhiteSpace(book.Title) ||
            string.IsNullOrWhiteSpace(book.Author) ||
            string.IsNullOrWhiteSpace(book.Isbn))
        {
            await DisplayAlert(
                "Missing information",
                "Title, author, ISBN, and publication year are required.",
                "OK");
            return;
        }

        try
        {
            _repository.Add(book);
            ClearBookForm();
            LoadBooks();
            await DisplayAlert(
                "Added",
                $"“{book.Title}” is now in the catalog.",
                "OK");
        }
        catch (LiteException exception)
            when (exception.ErrorCode == LiteException.INDEX_DUPLICATE_KEY)
        {
            await DisplayAlert(
                "ISBN already exists",
                "Each book in the catalog must have a unique ISBN.",
                "OK");
        }
        catch (Exception exception)
        {
            await ShowOperationErrorAsync("add the book", exception);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) =>
        LoadBooks();

    private void OnOpenStatusReportClicked(object sender, EventArgs e)
    {
        HideLoanForm();
        LostBooksOverlay.IsVisible = false;
        LoadStatusReport();
        StatusReportOverlay.IsVisible = true;
    }

    private void OnCloseStatusReportClicked(object sender, EventArgs e) =>
        StatusReportOverlay.IsVisible = false;

    private void OnOpenLostBooksClicked(object sender, EventArgs e)
    {
        LoadLostBooksReport();
        LostBooksOverlay.IsVisible = true;
    }

    private void OnCloseLostBooksClicked(object sender, EventArgs e) =>
        LostBooksOverlay.IsVisible = false;

    private void OnBookSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        _selectedBook = e.CurrentSelection.FirstOrDefault() as Book;
        HideLoanForm();
        ShowSelectedBook();
    }

    private void OnLendBookClicked(object sender, EventArgs e)
    {
        if (_selectedBook?.Status != BookStatus.Available)
        {
            return;
        }

        BorrowerNameEntry.Text = string.Empty;
        BorrowerIdEntry.Text = string.Empty;
        ShowBorrowerInputRule();
        ExpectedReturnDatePicker.MinimumDate = DateTime.Today;
        ExpectedReturnDatePicker.Date = DateTime.Today.AddDays(14);
        LoanForm.IsVisible = true;
        BorrowerNameEntry.Focus();
    }

    private void OnCancelLoanClicked(object sender, EventArgs e) =>
        HideLoanForm();

    private void OnBorrowerNameTextChanged(object sender, TextChangedEventArgs e)
    {
        FilterBorrowerEntry(
            (Entry)sender,
            e.NewTextValue,
            BorrowerInputRules.FilterFullName,
            "Enter at least two names using letters and spaces only.");
    }

    private void OnBorrowerIdTextChanged(object sender, TextChangedEventArgs e)
    {
        FilterBorrowerEntry(
            (Entry)sender,
            e.NewTextValue,
            BorrowerInputRules.FilterId,
            "ID accepts numbers only.");
    }

    private async void OnConfirmLoanClicked(object sender, EventArgs e)
    {
        if (_selectedBook is null)
        {
            return;
        }

        var borrowerName = BorrowerInputRules.NormalizeFullName(BorrowerNameEntry.Text);
        var borrowerId = BorrowerIdEntry.Text?.Trim() ?? string.Empty;
        var expectedReturnDate = ExpectedReturnDatePicker.Date;

        if (string.IsNullOrWhiteSpace(borrowerName) ||
            string.IsNullOrWhiteSpace(borrowerId))
        {
            await DisplayAlert(
                "Borrower information required",
                "Enter the borrower’s full name and ID to complete the loan.",
                "OK");
            return;
        }

        if (!BorrowerInputRules.IsValidFullName(borrowerName))
        {
            await DisplayAlert(
                "Check the borrower name",
                "Enter at least two names using letters and spaces only.",
                "OK");
            return;
        }

        if (!BorrowerInputRules.IsValidId(borrowerId))
        {
            await DisplayAlert(
                "Check the borrower ID",
                "The borrower ID can contain only numbers.",
                "OK");
            return;
        }

        if (expectedReturnDate.Date < DateTime.Today)
        {
            await DisplayAlert(
                "Check the return date",
                "The expected return date cannot be in the past.",
                "OK");
            return;
        }

        try
        {
            var bookTitle = _selectedBook.Title;
            _repository.LendBook(
                _selectedBook.Id,
                borrowerName,
                borrowerId,
                expectedReturnDate);
            HideLoanForm();
            LoadBooks();
            await DisplayAlert(
                "Loan recorded",
                $"“{bookTitle}” is now checked out to {borrowerName}.",
                "OK");
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or ArgumentException)
        {
            LoadBooks();
            await DisplayAlert("Loan not completed", exception.Message, "OK");
        }
        catch (Exception exception)
        {
            await ShowOperationErrorAsync("record the loan", exception);
        }
    }

    private async void OnReturnBookClicked(object sender, EventArgs e)
    {
        if (_selectedBook?.Status != BookStatus.CheckedOut)
        {
            return;
        }

        LoanRecord? activeLoan;
        try
        {
            activeLoan = _repository.GetActiveLoan(_selectedBook.Id);
        }
        catch (Exception exception)
        {
            await ShowOperationErrorAsync("load the active loan", exception);
            return;
        }
        var borrower = activeLoan?.BorrowerFullName ?? "the current borrower";
        var confirmed = await DisplayAlert(
            "Mark this book as returned?",
            $"This will record today as the actual return date and close the loan for {borrower}.",
            "Mark returned",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        try
        {
            var bookTitle = _selectedBook.Title;
            _repository.ReturnBook(_selectedBook.Id);
            LoadBooks();
            await DisplayAlert(
                "Return recorded",
                $"“{bookTitle}” is available to lend again.",
                "OK");
        }
        catch (InvalidOperationException exception)
        {
            LoadBooks();
            await DisplayAlert("Return not completed", exception.Message, "OK");
        }
        catch (Exception exception)
        {
            await ShowOperationErrorAsync("record the return", exception);
        }
    }

    private async void OnMarkBookLostClicked(object sender, EventArgs e)
    {
        if (_selectedBook?.Status != BookStatus.CheckedOut)
        {
            return;
        }

        var confirmed = await DisplayAlert(
            "Mark this book as lost?",
            "Marking a book as lost will remove it from the current library catalog and move it into Lost books.",
            "Mark as lost",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        try
        {
            var bookTitle = _selectedBook.Title;
            _repository.MarkBookLost(_selectedBook.Id);
            _selectedBook = null;
            HideLoanForm();
            LoadBooks();
            await DisplayAlert(
                "Book marked as lost",
                $"“{bookTitle}” was moved to the Lost books report.",
                "OK");
        }
        catch (InvalidOperationException exception)
        {
            LoadBooks();
            await DisplayAlert("Book not marked as lost", exception.Message, "OK");
        }
        catch (Exception exception)
        {
            await ShowOperationErrorAsync("mark the book as lost", exception);
        }
    }

    private async void OnDeleteBookClicked(object sender, EventArgs e)
    {
        if (_selectedBook is null)
        {
            return;
        }

        if (!await DisplayAlert(
                "Delete this book?",
                "This will permanently remove the book from the catalog.",
                "Delete",
                "Cancel"))
        {
            return;
        }

        try
        {
            _repository.Delete(_selectedBook.Id);
            _selectedBook = null;
            BooksCollection.SelectedItem = null;
            HideLoanForm();
            LoadBooks();
        }
        catch (InvalidOperationException exception)
        {
            LoadBooks();
            await DisplayAlert("Book not deleted", exception.Message, "OK");
        }
        catch (Exception exception)
        {
            await ShowOperationErrorAsync("delete the book", exception);
        }
    }

    private void OnClearFormClicked(object sender, EventArgs e) =>
        ClearBookForm();

    private void OnNewBookClicked(object sender, EventArgs e)
    {
        ClearBookForm();
        TitleEntry.Focus();
    }

    private void ClearBookForm()
    {
        TitleEntry.Text = string.Empty;
        AuthorEntry.Text = string.Empty;
        IsbnEntry.Text = string.Empty;
        YearEntry.Text = string.Empty;
    }

    private void HideLoanForm()
    {
        LoanForm.IsVisible = false;
        BorrowerNameEntry.Text = string.Empty;
        BorrowerIdEntry.Text = string.Empty;
        ShowBorrowerInputRule();
    }

    private void FilterBorrowerEntry(
        Entry entry,
        string? newValue,
        Func<string?, string> filter,
        string errorMessage)
    {
        if (_isFilteringBorrowerInput)
        {
            return;
        }

        var originalValue = newValue ?? string.Empty;
        var filteredValue = filter(originalValue);
        if (filteredValue == originalValue)
        {
            return;
        }

        _isFilteringBorrowerInput = true;
        entry.Text = filteredValue;
        entry.CursorPosition = filteredValue.Length;
        _isFilteringBorrowerInput = false;
        BorrowerInputRuleLabel.Text = errorMessage;
        BorrowerInputRuleLabel.TextColor = Color.FromArgb("#B5493B");
    }

    private void ShowBorrowerInputRule()
    {
        BorrowerInputRuleLabel.Text =
            "Full name: at least two names, letters and spaces only. ID: numbers only.";
        BorrowerInputRuleLabel.TextColor = Color.FromArgb("#776A5E");
    }

    private void ShowSelectedBook()
    {
        try
        {
            ShowSelectedBookCore();
        }
        catch (Exception exception)
        {
            QueueOperationError("load the selected book details", exception);
        }
    }

    private void ShowSelectedBookCore()
    {
        var book = _selectedBook;
        var activeLoan = book is null
            ? null
            : _repository.GetActiveLoan(book.Id);

        DetailTitleLabel.Text = book?.Title ?? "Select a book";
        DetailAuthorLabel.Text =
            book?.Author ?? "Its full catalog record will appear here.";
        DetailIsbnLabel.Text = $"ISBN  {book?.Isbn ?? "—"}";
        DetailYearLabel.Text =
            $"Published  {(book is null ? "—" : book.PublicationYear)}";
        DetailStatusLabel.Text = $"Status  {book?.StatusLabel ?? "—"}";
        DetailStatusLabel.TextColor = book?.Status switch
        {
            BookStatus.Available => Color.FromArgb("#9CC9A8"),
            BookStatus.CheckedOut => Color.FromArgb("#F08A7C"),
            BookStatus.Lost => Color.FromArgb("#E3B18C"),
            _ => Color.FromArgb("#E3B18C")
        };
        DetailLoanDateLabel.Text =
            $"Lent on  {activeLoan?.LentOn.ToString("g") ?? "—"}";
        DetailReturnDateLabel.Text =
            $"Expected return  {activeLoan?.ExpectedReturnDate.ToString("MMMM d, yyyy") ?? "—"}";
        DetailBorrowerLabel.Text =
            $"Borrower  {activeLoan?.BorrowerFullName ?? "—"}";
        DetailBorrowerIdLabel.Text =
            $"Borrower ID  {activeLoan?.BorrowerId ?? "—"}";

        var canLend = book?.Status == BookStatus.Available;
        LendButton.IsEnabled = canLend;
        LendButton.IsVisible = canLend;
        LostButton.IsVisible = book?.Status == BookStatus.CheckedOut;
        LostButton.IsEnabled = activeLoan is not null;
        ReturnButton.IsVisible = book?.Status == BookStatus.CheckedOut;
        ReturnButton.IsEnabled = activeLoan is not null;
        DeleteButton.IsEnabled = book?.Status == BookStatus.Available;
        LoadLoanHistory(book);
    }

    private void LoadLoanHistory(Book? book)
    {
        _loanHistory.Clear();

        if (book is not null)
        {
            foreach (var loan in _repository.GetLoanHistory(book.Id))
            {
                _loanHistory.Add(loan);
            }
        }

        var recordCount = _loanHistory.Count;
        LoanHistoryCountLabel.Text = recordCount == 1
            ? "1 record"
            : $"{recordCount} records";
        LoanHistoryList.IsVisible = recordCount > 0;
        NoLoanHistoryLabel.IsVisible = recordCount == 0;
        NoLoanHistoryLabel.Text = book is null
            ? "Select a book to view its loan history."
            : "No loans have been recorded for this book.";
    }

    private void LoadStatusReport()
    {
        try
        {
            _availableBooks.Clear();
            _checkedOutBooks.Clear();

            foreach (var item in _repository.GetStatusReport())
            {
                if (item.Status == BookStatus.Available)
                {
                    _availableBooks.Add(item);
                }
                else
                {
                    _checkedOutBooks.Add(item);
                }
            }

            AvailableCountLabel.Text = _availableBooks.Count.ToString();
            CheckedOutCountLabel.Text = _checkedOutBooks.Count.ToString();
        }
        catch (Exception exception)
        {
            _availableBooks.Clear();
            _checkedOutBooks.Clear();
            QueueOperationError("load the status report", exception);
        }
    }

    private void LoadLostBooksReport()
    {
        try
        {
            _lostBooks.Clear();

            foreach (var item in _repository.GetLostBooksReport())
            {
                _lostBooks.Add(item);
            }

            LostBooksCountLabel.Text = _lostBooks.Count.ToString();
        }
        catch (Exception exception)
        {
            _lostBooks.Clear();
            QueueOperationError("load the lost-books report", exception);
        }
    }

    private void QueueOperationError(string operation, Exception exception) =>
        Dispatcher.Dispatch(async () =>
            await ShowOperationErrorAsync(operation, exception));

    private async Task ShowOperationErrorAsync(string operation, Exception exception)
    {
        _logger.LogError(exception, "Could not {Operation}.", operation);

        if (_isShowingOperationError)
        {
            return;
        }

        _isShowingOperationError = true;
        try
        {
            var message = exception is LibraryDataException
                ? exception.Message
                : $"The app could not {operation}. Please try again. If the problem continues, restart the app.";
            await DisplayAlert("Something went wrong", message, "OK");
        }
        finally
        {
            _isShowingOperationError = false;
        }
    }
}
