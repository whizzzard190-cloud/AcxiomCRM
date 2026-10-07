namespace AcxiomCRM.ViewModels;

public class PagerModel
{
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalRecords { get; set; } = 0;
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
    public string Action { get; set; } = "Index";
    public string? Controller { get; set; }
    public Dictionary<string, string> RouteValues { get; set; } = new();

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}

public class PagedList<T>
{
    public List<T> Items { get; set; } = new();
    public PagerModel Pager { get; set; } = new();
}
