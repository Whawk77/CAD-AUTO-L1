using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace AutoFixtureDim;

public sealed class PluginEntry : IExtensionApplication
{
	public void Initialize()
	{
		Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
		if (mdiActiveDocument != null)
		{
			mdiActiveDocument.Editor.WriteMessage("\nAutoFixtureDim 已加载，命令: AUTOFIXDIM。");
		}
	}

	public void Terminate()
	{
	}
}
