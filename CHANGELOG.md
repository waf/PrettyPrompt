# Release 4.1.0

- Handle invalid history entries / history log corruption ([#267](https://github.com/waf/PrettyPrompt/pull/267)).

# Release 4.0.9

- Better error messages on Linux when xsel is not installed ([#264](https://github.com/waf/PrettyPrompt/pull/264)).
- Fix crash when Shift-Delete is pressed under certain conditions ([#263](https://github.com/waf/PrettyPrompt/pull/263)).
- Add workaround for garbled utf-8 characters on Linux ([#261](https://github.com/waf/PrettyPrompt/pull/261)).

# Release 4.0.8

- Fix AltGr handling in non-QWERTY keyboards ([#259](https://github.com/waf/PrettyPrompt/pull/259)).

# Release 4.0.7

- Support viewport scrolling if input content is longer than console height

# Release 4.0.6

- Bugfix: if the control modifier is pressed, don't insert the character ([#252](https://github.com/waf/PrettyPrompt/pull/252)).

# Release 4.0.5

- Add a new type that be returned by keybinding callbacks: `StreamingInputCallbackResult`. This allows `IAsyncEnumerable<string>` to be rendered into the prompt ([#249](https://github.com/waf/PrettyPrompt/pull/249)).

# Release 4.0.4

- Improved completion item ordering + case sensitive filtering ([#244](https://github.com/waf/PrettyPrompt/pull/244)).
- Fix of the problem where oveload pane could stay on screen after input submission ([#239](https://github.com/waf/PrettyPrompt/issues/239)).
- Change of unexpected character `\r` to `\n` in `ConsoleKeyInfo` when pressing `Enter` ([#242](https://github.com/waf/PrettyPrompt/issues/242)).

# Release 4.0.3

- Ignore errors formatting when error stream is redirected ([#238](https://github.com/waf/PrettyPrompt/pull/238)).

# Release 4.0.2

- Fix of invalid behaviour when user used more lines than `Console.BufferHeight` ([#228](https://github.com/waf/PrettyPrompt/issues/228)).
- Fix of invalid positioning of completion pane for "scrolling inputs" ([#229](https://github.com/waf/PrettyPrompt/issues/229)).
- Not drawing empty documentation box when no completion item is selected ([#232](https://github.com/waf/PrettyPrompt/issues/232)).
- `PromptConfiguration.Prompt` is now editable ([#235](https://github.com/waf/PrettyPrompt/issues/235)).

# Release 4.0.1

- Fix of not enough space for completion panes in multiline statements ([#223](https://github.com/waf/PrettyPrompt/issues/223)).
- Support for writing of FormattedString to error stream. `IConsole.WriteError(FormattedString)` and `IConsole.WriteErrorLine(FormattedString)`.

# Release 4.0.0

This release contains many new features, performance improvements, and bugfixes developed by contributor @kindermannhubert.

- Breaking change: Target .NET 6 instead of .NET 5 ([#202](https://github.com/waf/PrettyPrompt/pull/202)).
- Breaking change: Correct awaiting of history saving ([#201](https://github.com/waf/PrettyPrompt/pull/201)).
    - Before, saving of history was fire-and-forget, which could mean that in certain race condition scenarios history would not be properly saved.
    - To fix this, the prompt now implements `IAsyncDisposable` and should be disposed after use to guarantee that history is always saved.
- Overload help support! In addition to the existing intellisense-style autocompletions, PrettyPrompt now supports displaying "overload menus" that can be navigated with the arrow keys, similar to Visual Studio ([#209](https://github.com/waf/PrettyPrompt/pull/209)).
- Fix of WordWrapping removing empty lines. ([#204](https://github.com/waf/PrettyPrompt/pull/204)).
- Add a new configuration method: `IPromptCallbacks.ConfirmCompletionCommit`. This allows completions to be accepted / rejected when they are about to be inserted, based on the position of the caret in the text. It's useful if a completion would be automatically inserted while the user is typing ([#212](https://github.com/waf/PrettyPrompt/pull/212)).
- Support for automatic formatting of the input text as it's being typed. See `IPromptCallbacks.FormatInput` ([#213](https://github.com/waf/PrettyPrompt/pull/213)).
- Support for indentation changing of multiple selected lines via Tab and Shift+Tab ([#214](https://github.com/waf/PrettyPrompt/pull/214)).

# Release 3.0.6

- Configurable Keybindings for history scrolling ([#197](https://github.com/waf/PrettyPrompt/pull/197))
- Persistent history deduplication ([#189](https://github.com/waf/PrettyPrompt/pull/189))
- Better history scrolling in multiline statements ([#181](https://github.com/waf/PrettyPrompt/issues/181), [#193](https://github.com/waf/PrettyPrompt/pull/193))
- Ensure scrolling forward/backwards through filtered history provides consistent results ([#192](https://github.com/waf/PrettyPrompt/pull/192))
- Smarter history filtering ([#195](https://github.com/waf/PrettyPrompt/pull/195), [#196](https://github.com/waf/PrettyPrompt/pull/196))
- When a ConsoleKey KeyPressPattern is provided in a keybinding, map the ConsoleKey to a char ([#199](https://github.com/waf/PrettyPrompt/pull/199))

# Release 3.0.5

- Performance improvement of IConsole.Write(FormattedString).
- `IPromptCallbacksShouldOpenCompletionWindowAsync` now accepts also `KeyPress` argument.
- `CompletionItem` has new property `CommitCharacterRules` which modifies configured global commit characters.
- Fix of incorrect rendering of description box in multiline statements ([#149](https://github.com/waf/PrettyPrompt/issues/149)).
- Win/F1/F2/... keys do not deselect currently selected text ([#156](https://github.com/waf/PrettyPrompt/issues/156)).
- Fix of Home press on empty line ([#161](https://github.com/waf/PrettyPrompt/issues/161)).
- Fix of down arrow not working properly when last character is '\n' ([#160](https://github.com/waf/PrettyPrompt/issues/160)).
- Improved selection formatting + it's configurable ([#155](https://github.com/waf/PrettyPrompt/issues/155)).
- Ctrl+X when nothing is selected cuts the current line ([#151](https://github.com/waf/PrettyPrompt/issues/151)).
- Current line can be deleted with Shift+Delete ([#152](https://github.com/waf/PrettyPrompt/issues/152)).
- Fix of indentation removal inside of pasted text. It's removed only when there are multiple non-whitespace lines ([#168](https://github.com/waf/PrettyPrompt/issues/168)).
- Fix of key-binding matching ([#147](https://github.com/waf/PrettyPrompt/issues/147)).
- Fix of multiple '\r\n' pasting ([#166](https://github.com/waf/PrettyPrompt/issues/166)).
- Fixes other minor bugs.

# Release 3.0.4

- Fix of insufficient formatting reseting in extension method `IConsole.Write(FormattedString)`.

# Release 3.0.3

- Fix of incorrect positioning of cursor while using 2-character symbols ([#134](https://github.com/waf/PrettyPrompt/issues/134)).
- Fix of crash when repeating deleting selected text and Ctrl+Z ([#139](https://github.com/waf/PrettyPrompt/pull/139)).
- Width fix of some emoji characters.
- Fixes of around text selection and undo/redo.

# Release 3.0.2

- Fix of completion pane that should not open when selection is active ([#126](https://github.com/waf/PrettyPrompt/issues/126)).
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
