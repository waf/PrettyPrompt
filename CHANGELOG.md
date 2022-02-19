Release 3.0.0

This is a large release that greatly improves the usability, consistency, and reliability of PrettyPrompt. Special thanks to @kindermannhubert for an incredible amount of work to improve PrettyPrompt!

- Breaking Changes
  - Replace "Prompt Callback" delegates with virtual method overrides (see [discussion and rationale here](https://github.com/waf/PrettyPrompt/discussions/73).
    - For an example of how to adjust your configuration, see the [CSharpRepl PR here](https://github.com/waf/CSharpRepl/pull/63). Specifically, the conversion from the [PrettyPrompt 2.0 configuration](https://github.com/waf/CSharpRepl/blob/c8a2b603f0948d52b18e7c679b1695dcb85d2da9/CSharpRepl/PrettyPromptConfig/PromptConfiguration.cs) to the [PrettyPrompt 3.0 configuration](https://github.com/waf/CSharpRepl/blob/258b94a40b7e67b6662d5e5a834d3636afbcf9ed/CSharpRepl/CSharpReplPromptCallbacks.cs).
  - Move ANSI color properties to new `AnsiColor` struct. For example, `AnsiEscapeCodes.Red` changes to `AnsiColor.Red.GetEscapeSequence()`.
- Features and Fixes
  - Allow rich formatting in the completion suggestions and documentation
  - Rework cursor navigation and text selection to more closely match Visual Studio and other established applications.
  - Remove hard-coded colors in favor of configuration themes
  - Allow configurability of how the selected completion item is rendered (both the marker and highlighting)
  - Configurable tab size
  - Undo/redo bugfixes and improvements
  - Fix crashes related to text selection, manipulation, and window resizing
  - Fixes related to reflowing and wrapping of text inside the window
  - Implement "smart home" which allows the home key to work better with leading indentation.
  - Multiline paste improvements
  - Performance improvements and allocation reductions.
  - Nullability annotations!

Release 2.0.1

- Adds a new PromptCallback.OpenCompletionWindowCallback property for customizing completion window auto-open behavior.

Release 2.0

- Feature: Allow keyboard shortcuts to submit the prompt on behalf of the user.
- Breaking change: KeyPressCallbacks now return a Task<KeyPressCallbackResult> instead of a Task. To maintain the old behavior from v1.0, return a null KeyPressCallbackResult (e.g.  Task.FromResult<KeyPressCallbackResult>(null) ).

Release 1.0

- First release of PrettyPrompt
