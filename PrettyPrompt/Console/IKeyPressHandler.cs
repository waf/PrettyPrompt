using System.Threading.Tasks;

namespace PrettyPrompt
{
    internal interface IKeyPressHandler
    {
        Task<bool> OnKeyDown(KeyPress key);
        Task<bool> OnKeyUp(KeyPress key);
    }
}