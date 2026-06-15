using System;
using System.Threading;
using System.Threading.Tasks;
using TextCopy;

namespace PrettyPrompt.TextSelection;

/// <summary>
/// Wraps the <see cref="TextCopy"/> clipboard so that transient clipboard failures never crash the prompt.
/// The OS clipboard can be flaky for reasons outside our control - e.g. no X display available, or the
/// underlying helper process timing out (TextCopy shells out with a hard-coded 500ms timeout on Linux).
/// A cut/copy/paste hiccup like that should degrade gracefully rather than abort the whole prompt, so the
/// Try* methods report failure instead of throwing. Callers can then avoid destructive edits - e.g. not
/// removing cut text that never made it to the clipboard.
///
/// A missing clipboard executable (xsel/clip.exe) is different: it's an actionable setup problem rather
/// than a transient hiccup, so the Try* methods surface it as an exception with a helpful message instead
/// of silently swallowing it.
/// </summary>
internal sealed class WrappedClipboard
{
    private const string MissingExecutableError = "Could not execute process";
    private const string HelpfulErrorMessage = "Could not access clipboard. Check that xsel (Linux) or clip.exe (WSL) is installed.";

    private readonly IClipboard clipboard;

    public WrappedClipboard()
        : this(new Clipboard())
    {
    }

    public WrappedClipboard(IClipboard clipboard)
    {
        this.clipboard = clipboard;
    }

    /// <summary>Reads the clipboard. Returns <see langword="false"/> (with <paramref name="text"/> null) if the read failed.</summary>
    public bool TryGetText(out string? text)
    {
        try
        {
            text = clipboard.GetText();
            return true;
        }
        catch (Exception ex) when (IsMissingExecutable(ex))
        {
            throw new Exception(HelpfulErrorMessage, ex);
        }
        catch
        {
            text = null;
            return false;
        }
    }

    /// <summary>Reads the clipboard. <c>Success</c> is <see langword="false"/> (and <c>Text</c> null) if the read failed.</summary>
    public async Task<(bool Success, string? Text)> TryGetTextAsync(CancellationToken cancellation = default)
    {
        try
        {
            return (true, await clipboard.GetTextAsync(cancellation).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsMissingExecutable(ex))
        {
            throw new Exception(HelpfulErrorMessage, ex);
        }
        catch
        {
            return (false, null);
        }
    }

    /// <summary>Writes to the clipboard. Returns <see langword="false"/> if the write failed.</summary>
    public bool TrySetText(string text)
    {
        try
        {
            clipboard.SetText(text);
            return true;
        }
        catch (Exception ex) when (IsMissingExecutable(ex))
        {
            throw new Exception(HelpfulErrorMessage, ex);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Writes to the clipboard. Returns <see langword="false"/> if the write failed.</summary>
    public async Task<bool> TrySetTextAsync(string text, CancellationToken cancellation = default)
    {
        try
        {
            await clipboard.SetTextAsync(text, cancellation).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsMissingExecutable(ex))
        {
            throw new Exception(HelpfulErrorMessage, ex);
        }
        catch
        {
            return false;
        }
    }

    // A missing clipboard executable is reported by TextCopy as a "Could not execute process" error;
    // unlike a timeout, reinstalling the tool is the fix, so we surface it rather than swallow it.
    private static bool IsMissingExecutable(Exception ex) => ex.Message.Contains(MissingExecutableError);
}
