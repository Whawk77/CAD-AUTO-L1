using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace AutoFixtureDim;

public sealed class PluginEntry : IExtensionApplication
{
	public void Initialize()
	{
		try
		{
			Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
			if (mdiActiveDocument != null)
			{
				mdiActiveDocument.Editor.WriteMessage(
					"\nAutoFixtureDim 已加载。主命令 ASD；其它: ASD3/ASD4/ASD5/ASD6/ASD7/ASDCOREDBG/ASDREPRO/AUTOFIXDIM/AG1。程序集: {0}",
					typeof(PluginEntry).Assembly.Location);
			}
		}
		catch (System.Exception)
		{
			// Never block NETLOAD over a banner failure.
		}
	}

	public void Terminate()
	{
	}
}
