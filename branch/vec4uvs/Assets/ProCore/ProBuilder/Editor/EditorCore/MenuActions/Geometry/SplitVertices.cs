using UnityEngine;
using UnityEditor;
using ProBuilder2.Common;
using ProBuilder2.EditorCommon;
using ProBuilder2.Interface;
using System.Linq;

namespace ProBuilder2.Actions
{
	public class SplitVertices : pb_MenuAction
	{
		public override pb_IconGroup group { get { return pb_IconGroup.Geometry; } }
		public override Texture2D icon { get { return pb_IconUtility.GetIcon("Vert_Split"); } }
		public override pb_TooltipContent tooltip { get { return _tooltip; } }

		static readonly pb_TooltipContent _tooltip = new pb_TooltipContent
		(
			"Split Vertices",
			@"Disconnects vertices that share the same position in space so that they may be moved independently of one another.",
			CMD_ALT, 'X'
		);

		public override bool IsEnabled()
		{
			return 	pb_Editor.instance != null &&
					pb_Editor.instance.editLevel == EditLevel.Geometry &&
					pb_Editor.instance.selectionMode == SelectMode.Vertex &&
					selection != null &&
					selection.Length > 0 &&
					selection.Any(x => x.SelectedTriangleCount > 0);
		}
		
		public override bool IsHidden()
		{
			return 	pb_Editor.instance == null ||
					pb_Editor.instance.editLevel != EditLevel.Geometry ||
					pb_Editor.instance.selectionMode != SelectMode.Vertex;
					
		}

		public override pb_ActionResult DoAction()
		{
			return pb_Menu_Commands.MenuSplitVertices(selection);
		}
	}
}