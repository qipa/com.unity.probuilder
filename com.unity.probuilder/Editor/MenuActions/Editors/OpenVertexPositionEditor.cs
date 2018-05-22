﻿using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder.UI;

namespace UnityEditor.ProBuilder.Actions
{
	sealed class OpenVertexPositionEditor : MenuAction
	{
		public override ToolbarGroup group { get { return ToolbarGroup.Tool; } }
		public override Texture2D icon { get { return null; } }
		public override TooltipContent tooltip { get { return _tooltip; } }
		public override string menuTitle { get { return "Vertex Editor"; } }

		static readonly TooltipContent _tooltip = new TooltipContent
		(
			"Vertex Position Editor",
			"Opens the vertex positions editor window."
		);

		public override bool IsEnabled()
		{
			return ProBuilderEditor.instance != null;
		}

		public override bool IsHidden()
		{
			return true;
		}

		public override ActionResult DoAction()
		{
			VertexPositionEditor.MenuOpenVertexEditor();
			return new ActionResult(ActionResult.Status.Success, "Open Vertex Editor Window");
		}
	}
}