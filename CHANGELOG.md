# Release 3.0.4

- Fix of insufficient formatting reseting in extension method `IConsole.Write(FormattedString)`.

# Release 3.0.3

- Fix of incorrect positioning of cursor while using 2-character symbols (#134).
- Fix of crash when repeating deleting selected text and Ctrl+Z (#139).
- Width fix of some emoji characters.
- Fixes of around text selection and undo/redo.

# Release 3.0.2

- Fix of completion pane that should not open when selection is active (#126).
- Upgrade of referenced NuGets.
- Minor enhancements.

# Release 3.0.1

- `IPromptCallbacks.InterpretKeyPressAsInputSubmitAsync` -> `IPromptCallbacks.TransformKeyPressAsync`

# Release 3.0.0

This is a large release that greatly improves the usability, consistency, and reliability of PrettyPrompt. Special thanks to @kindermannhubert for an incredible amount of work to improve PrettyPrompt!

- Breaking Changes:
  - Replace "Prompt Callback" delegates with virtual method overrides (see [discussion and rationale here](https://github.com/waf/PrettyPrompt/discussions/73)).
    - For an example of how to adjust your configuration, see the [CSharpRepl PR here](https://github.com/waf/CSharpRepl/pull/63). Specifically, the conversion from the [PrettyPrompt 2.0 configuration](https://github.com/waf/CSharpRepl/blob/c8a2b603f0948d52b18e7c679b1695dcb85d2da9/CSharpRepl/PrettyPromptConfig/PromptConfiguration.cs) to the [PrettyPrompt 3.0 configuration](https://github.com/waf/CSharpRepl/blob/258b94a40b7e67b6662d5e5a834d3636afbcf9ed/CSharpRepl/CSharpReplPromptCallbacks.cs).
  - Move ANSI color properties to new `AnsiColor` struct. For example, `AnsiEscapeCodes.Red` changes to `AnsiColor.Red.GetEscapeSequence()`.
  - `CompletionItem` API:
    - `int StartIndex` - removed in favor of `PromptCallbacks.GetSpanToReplaceByCompletionkAsync`, which has default but overridable implementation.
    - `string DisplayText` -> `FormattedString DisplayTextFormatted`.
    - `string FilterText` - new and used for item matching.
    - `Lazy<Task<string>> ExtendedDescription` - changed to delegate of form `Task<FormattedString> GetExtendedDescription(CancellationToken cancellationToken)`.
  - More minor changes.
- Features and Fixes:
  - Allow rich formatting in the completion suggestions and documentation.
  - Rework cursor navigation and text selection to more closely match Visual Studio and other established applications.
  - Undo/redo bugfixes and improvements.
  - Fix crashes related to text selection, manipulation, and window resizing.
  - Fixes related to reflowing and wrapping of text inside the window.
  - Implement "smart home" which allows the home key to work better with leading indentation.
  - Multiline paste improvements.
  - Performance improvements and allocation reductions.
  - Nullability annotations.
  - Improved completion item matching and priority ordering.
  - Completion list contains also non-matching items (bellow matching ones).
  - Configurability:
    - Prompt.
    - Rendering colors.
    - Selected completion item rendering (both the marker and highlighting).
    - Tab size.
    - Min/max number of items in completion list.
    - Maximal proportion of window height for completion list.
    - Key bindings (commit completion, trigger completion list, new line, submit prompt).

# Release 2.0.1

- Adds a new PromptCallback.OpenCompletionWindowCallback property for customizing completion window auto-open behavior.

# Release 2.0

- Breaking change: KeyPressCallbacks now return a Task<KeyPressCallbackResult> instead of a Task. To maintain the old behavior from v1.0, return a null KeyPressCallbackResult (e.g.  Task.FromResult<KeyPressCallbackResult>(null) ).
- Feature: Allow keyboard shortcuts to submit the prompt on behalf of the user.

# Release 1.0

- First release of PrettyPrompt.
