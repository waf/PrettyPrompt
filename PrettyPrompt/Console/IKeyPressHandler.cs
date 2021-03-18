using System.Threading.Tasks;

namespace PrettyPrompt
{
    internal interface IKeyPressHandler
    {
        Task OnKeyDown(KeyPress key);
        Task OnKeyUp(KeyPress key);
    }
}