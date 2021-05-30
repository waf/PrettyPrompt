#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Threading.Tasks;

namespace PrettyPrompt.Consoles
{
    internal interface IKeyPressHandler
    {
        Task OnKeyDown(KeyPress key);
        Task OnKeyUp(KeyPress key);
    }
}