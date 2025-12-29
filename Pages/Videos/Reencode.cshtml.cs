using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using GrinVideoEncoder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Pages.Videos;

public class ReencodeModel : PageModel
{
    private readonly VideoIndexerDbContext _context;
    private readonly CommunicationService _comm;

    public ReencodeModel(VideoIndexerDbContext context, CommunicationService comm)
    {
        _context = context;
		_comm = comm;
    }

    [BindProperty(SupportsGet = true)]
    public double Threshold { get; set; } = 900.0;
    public List<VideoFile> Videos { get; set; } = new();

    public async Task OnGetAsync()
    {
        Videos = await _context.GetVideosWithHighQualityRatioAsync(Threshold);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Videos = await _context.GetVideosWithHighQualityRatioAsync(Threshold);
        if (Videos.Count > 0)
        {
			foreach (var item in Videos)
			{
				_comm.VideoToProcess.Push(item.Id);
			}
        }
        return Page();
    }
}
