using System;
using System.Threading;
using System.Threading.Tasks;
using TextCopy;

namespace PrettyPrompt.TextSelection;
internal class WrappedClipboard : IClipboard
{
    private const string MissingExecutableError = "Could not execute process";
    private const string HelpfulErrorMessage = "Could not access clipboard. Check that xsel (Linux) or clip.exe (WSL) is installed.";
    private readonly Clipboard clipboard;

    public WrappedClipboard()
    {
        this.clipboard = new Clipboard();
    }

    public string? GetText()
    {
        try
        {
            return clipboard.GetText();
        }
        catch (Exception ex) when (ex.Message.Contains(MissingExecutableError))
        {
            throw new Exception(HelpfulErrorMessage, ex);
        }
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellation = default)
    {
        try
        {
            return await clipboard.GetTextAsync(cancellation).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.Message.Contains(MissingExecutableError))
        {
            throw new Exception(HelpfulErrorMessage, ex);
        }
    }

    public void SetText(string text)
    {
        try
        {
            clipboard.SetText(text);
        }
        catch (Exception ex) when (ex.Message.Contains(MissingExecutableError))
        {
            throw new Exception(HelpfulErrorMessage, ex);
        }
    }

    public async Task SetTextAsync(string text, CancellationToken cancellation = default)
    {
        try
        {
            await clipboard.SetTextAsync(text, cancellation).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.Message.Contains(MissingExecutableError))
        {
            throw new Exception(HelpfulErrorMessage, ex);
        }
    }
}
