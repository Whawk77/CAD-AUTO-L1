using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace AutoFixtureDim
{
    public sealed class PluginEntry : IExtensionApplication
    {
        public void Initialize()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                document.Editor.WriteMessage("\nAutoFixtureDim 已加载，命令: AUTOFIXDIM。");
            }
        }

        public void Terminate()
        {
        }
    }
}
