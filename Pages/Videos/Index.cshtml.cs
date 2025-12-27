using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Pages.Videos;

public class IndexModel : PageModel
{
    private readonly VideoIndexerDbContext _context;
    private const int PageSize = 50;

    public IndexModel(VideoIndexerDbContext context)
    {
        _context = context;
    }

    public IList<VideoFile> Videos { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DirectoryFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? MinSizeMB { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? MaxSizeMB { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "Filename";

    [BindProperty(SupportsGet = true)]
    public bool SortDescending { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public async Task OnGetAsync()
    {
        var query = _context.VideoFiles.AsQueryable();

        // Filter by filename
        if (!string.IsNullOrEmpty(SearchString))
        {
            query = query.Where(v => v.Filename.Contains(SearchString));
        }

        // Filter by directory
        if (!string.IsNullOrEmpty(DirectoryFilter))
        {
            query = query.Where(v => v.DirectoryPath.Contains(DirectoryFilter));
        }

        // Filter by date range
        if (DateFrom.HasValue)
        {
            query = query.Where(v => v.IndexedAt >= DateFrom.Value);
        }

        if (DateTo.HasValue)
        {
            var dateToEnd = DateTo.Value.AddDays(1);
            query = query.Where(v => v.IndexedAt < dateToEnd);
        }

        // Filter by file size (convert MB to bytes)
        if (MinSizeMB.HasValue)
        {
            var minBytes = MinSizeMB.Value * 1024 * 1024;
            query = query.Where(v => v.FileSizeOriginal >= minBytes);
        }

        if (MaxSizeMB.HasValue)
        {
            var maxBytes = MaxSizeMB.Value * 1024 * 1024;
            query = query.Where(v => v.FileSizeOriginal <= maxBytes);
        }

        // Apply sorting
        query = SortBy switch
        {
            "Filename" => SortDescending ? query.OrderByDescending(v => v.Filename) : query.OrderBy(v => v.Filename),
            "Directory" => SortDescending ? query.OrderByDescending(v => v.DirectoryPath) : query.OrderBy(v => v.DirectoryPath),
            "Size" => SortDescending ? query.OrderByDescending(v => v.FileSizeOriginal) : query.OrderBy(v => v.FileSizeOriginal),
            "Duration" => SortDescending ? query.OrderByDescending(v => v.DurationSeconds) : query.OrderBy(v => v.DurationSeconds),
            "QualityRatio" => SortDescending 
                ? query.OrderByDescending(v => v.DurationSeconds.HasValue && v.DurationSeconds > 0 
                    ? (v.FileSizeCompressed.HasValue ? (double)v.FileSizeCompressed.Value : (double)v.FileSizeOriginal) / v.DurationSeconds.Value 
                    : (double?)null)
                : query.OrderBy(v => v.DurationSeconds.HasValue && v.DurationSeconds > 0 
                    ? (v.FileSizeCompressed.HasValue ? (double)v.FileSizeCompressed.Value : (double)v.FileSizeOriginal) / v.DurationSeconds.Value 
                    : (double?)null),
            "CompressionFactor" => SortDescending 
                ? query.OrderByDescending(v => v.FileSizeCompressed.HasValue && v.FileSizeOriginal > 0 
                    ? (double)v.FileSizeOriginal * 100.0 / v.FileSizeCompressed.Value 
                    : (double?)null)
                : query.OrderBy(v => v.FileSizeCompressed.HasValue && v.FileSizeOriginal > 0 
                    ? (double)v.FileSizeOriginal * 100.0 / v.FileSizeCompressed.Value 
                    : (double?)null),
            "IndexedAt" => SortDescending ? query.OrderByDescending(v => v.IndexedAt) : query.OrderBy(v => v.IndexedAt),
            _ => query.OrderBy(v => v.Filename)
        };

        // Get total count for pagination
        TotalCount = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);

        // Ensure valid page number
        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        // Apply pagination
        Videos = await query
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public string GetSortUrl(string column)
    {
        var newDescending = SortBy == column && !SortDescending;
        return $"?SearchString={SearchString}&DirectoryFilter={DirectoryFilter}&DateFrom={DateFrom:yyyy-MM-dd}&DateTo={DateTo:yyyy-MM-dd}&MinSizeMB={MinSizeMB}&MaxSizeMB={MaxSizeMB}&SortBy={column}&SortDescending={newDescending}&PageNumber=1";
    }

    public string GetSortIcon(string column)
    {
        if (SortBy != column) return "";
        return SortDescending ? "▼" : "▲";
    }
}
