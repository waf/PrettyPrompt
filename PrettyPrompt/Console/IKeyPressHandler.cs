using System.Threading.Tasks;

namespace PrettyPrompt.Consoles
{
    internal interface IKeyPressHandler
    {
        Task OnKeyDown(KeyPress key);
        Task OnKeyUp(KeyPress key);
    }
}