using UnityEngine;
using UnityEditor;
using ProBuilder2.Common;
using System.Collections.Generic;
using ProBuilder2.Interface;

namespace ProBuilder2.EditorCommon
{
	[CustomEditor(typeof(pb_PolyShape))]
	public class pb_PolyShapeTool : Editor
	{
		private static Color HANDLE_COLOR = new Color(.8f, .8f, .8f, 1f);
		private static Color HANDLE_GREEN = new Color(.01f, .9f, .3f, 1f);
		private static Color SELECTED_COLOR = new Color(.01f, .8f, .98f, 1f);

		[SerializeField] private Material m_LineMaterial;
		private Mesh m_LineMesh = null;
		private Plane m_Plane = new Plane(Vector3.up, Vector3.zero);
		private bool m_PlacingPoint = false;
		private int m_SelectedIndex = -2;
		private float m_DistanceFromHeightHandle;
		private bool m_NextMouseUpAdvancesMode = false;
		private List<GameObject> m_IgnorePick = new List<GameObject>();
		// private HashSet<int> m_SelectedIndices = new HashSet<int>();

		private pb_PolyShape polygon { get { return target as pb_PolyShape; } }

		void OnEnable()
		{
			pb_Editor.AddOnEditLevelChangedListener(OnEditLevelChange);
			m_LineMesh = new Mesh();
			Undo.undoRedoPerformed += UndoRedoPerformed;
			DrawPolyLine(polygon.points);
			EditorApplication.update += Update;

			if(pb_Editor.instance && polygon.polyEditMode != pb_PolyShape.PolyEditMode.None)
				pb_Editor.instance.SetEditLevel(EditLevel.Plugin);
		}

		void OnDisable()
		{
			pb_Editor.RemoveOnEditLevelChangedListener(OnEditLevelChange);
			GameObject.DestroyImmediate(m_LineMesh);
			EditorApplication.update -= Update;
			Undo.undoRedoPerformed -= UndoRedoPerformed;
		}

		public override void OnInspectorGUI()
		{
			switch(polygon.polyEditMode)
			{
				case pb_PolyShape.PolyEditMode.None:
				{
					if( GUILayout.Button("Edit Poly Shape") )
						SetPolyEditMode(pb_PolyShape.PolyEditMode.Edit);

					EditorGUILayout.HelpBox("Editing a poly shape will erase any modifications made to the mesh!\n\nIf you accidentally enter Edit Mode you can Undo to get your changes back.", MessageType.Warning);

					break;
				}

				case pb_PolyShape.PolyEditMode.Path:
				{
					EditorGUILayout.HelpBox("\nClick To Add Points\n\nPress 'Enter' or 'Space' to Set Height\n", MessageType.Info);
					break;
				}

				case pb_PolyShape.PolyEditMode.Height:
				{
					EditorGUILayout.HelpBox("\nMove Mouse to Set Height\n\nPress 'Enter' or 'Space' to Finalize\n", MessageType.Info);
					break;
				}

				case pb_PolyShape.PolyEditMode.Edit:
				{
					if( GUILayout.Button("Editing Poly Shape", pb_GUI_Utility.GetActiveStyle("Button")) )
						SetPolyEditMode(pb_PolyShape.PolyEditMode.None);
					break;
				}

			}

			EditorGUI.BeginChangeCheck();
			
			float extrude = polygon.extrude;
			extrude = EditorGUILayout.FloatField("Extrusion", extrude);

			bool flipNormals = polygon.flipNormals;
			flipNormals = EditorGUILayout.Toggle("Flip Normals", flipNormals);

			if(EditorGUI.EndChangeCheck())
			{
				if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.None)
				{
					if(pb_Editor.instance != null)
						pb_Editor.instance.ClearElementSelection();

					pbUndo.RecordObject(polygon, "Change Polygon Shape Settings");
					pbUndo.RecordObject(polygon.mesh, "Change Polygon Shape Settings");
				}
				else
				{
					pbUndo.RecordObject(polygon, "Change Polygon Shape Settings");
				}

				polygon.extrude = extrude;
				polygon.flipNormals = flipNormals;
				UpdateMesh();
			}

			// GUILayout.Label("selected : " + m_SelectedIndex);
		}

		void Update()
		{
			if(m_LineMaterial != null)
				m_LineMaterial.SetFloat("_EditorTime", (float) EditorApplication.timeSinceStartup);
		}

		void SetPolyEditMode(pb_PolyShape.PolyEditMode mode)
		{
			if(mode != polygon.polyEditMode)
			{
				// Clear the control always
				GUIUtility.hotControl = 0;

				// Entering edit mode after the shape has been finalized once before, which means
				// possibly reverting manual changes.  Store undo state so that if this was
				// not intentional user can revert.
				if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.None && polygon.points.Count > 2)
				{
					if(pb_Editor.instance != null)
						pb_Editor.instance.ClearElementSelection();

					pbUndo.RecordObject(polygon, "Edit Polygon Shape");
					pbUndo.RecordObject(polygon.mesh, "Edit Polygon Shape");
				}

				polygon.polyEditMode = mode;

				if(pb_Editor.instance != null)
				{
					if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.None)
						pb_Editor.instance.PopEditLevel();
					else
						pb_Editor.instance.SetEditLevel(EditLevel.Plugin);
				}

				if(polygon.polyEditMode != pb_PolyShape.PolyEditMode.None)
					Tools.current = Tool.None;

				UpdateMesh();
			}
			else
			{
				polygon.polyEditMode = mode;
			}
		}

		void SetPlane(Vector2 mousePosition)
		{
			GameObject go = null;
			m_IgnorePick.Clear();

			do
			{
				if(go != null)
					m_IgnorePick.Add(go);

				go = HandleUtility.PickGameObject(mousePosition, false, m_IgnorePick.ToArray());
			}
			while(go != null && go.GetComponent<MeshFilter>() == null);

			if(go != null)
			{
				Mesh m = go.GetComponent<MeshFilter>().sharedMesh;

				if(m != null)
				{
					pb_RaycastHit hit;

					if(pb_HandleUtility.WorldRaycast(HandleUtility.GUIPointToWorldRay(mousePosition),
						go.transform,
						m.vertices,
						m.triangles,
						out hit))
					{

						polygon.transform.position = go.transform.TransformPoint(hit.point);
						polygon.transform.rotation = Quaternion.LookRotation(go.transform.TransformDirection(hit.normal).normalized) * Quaternion.Euler(new Vector3(90f, 0f, 0f));
						polygon.isOnGrid = false;
						return;
					}
				}
			}

			// No mesh in the way, set the plane based on camera
			SceneView sceneView = SceneView.lastActiveSceneView;
			float cam_x = Vector3.Dot(sceneView.camera.transform.forward, Vector3.right);
			float cam_y = Vector3.Dot(sceneView.camera.transform.position - sceneView.pivot.normalized, Vector3.up);
			float cam_z = Vector3.Dot(sceneView.camera.transform.forward, Vector3.forward);

			ProjectionAxis axis = ProjectionAxis.Y;

			if( Mathf.Abs(cam_x) > .98f )
				axis = ProjectionAxis.X;
			else if ( Mathf.Abs(cam_z) > .98f )
				axis = ProjectionAxis.Z;
				
			if(pb_ProGrids_Interface.SnapEnabled())
				polygon.transform.position = pbUtil.SnapValue(polygon.transform.position, pb_ProGrids_Interface.SnapValue());

			switch(axis)
			{
				case ProjectionAxis.X:
					polygon.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 90f * Mathf.Sign(cam_x)));
					break;

				case ProjectionAxis.Y:
					polygon.transform.rotation = Quaternion.Euler(new Vector3(cam_y < 0f ? 180f : 0f, 0f, 0f));
					break;

				case ProjectionAxis.Z:
					polygon.transform.rotation = Quaternion.Euler(new Vector3(-90f * Mathf.Sign(cam_z), 0f, 0f));
					break;
			}
		}

		/**
		 *	Update the pb_Object with the new coordinates.  Returns true if mesh successfully triangulated, false if not.
		 */
		bool UpdateMesh(bool vertexCountChanged = true)
		{
			// If Undo is called immediately after creation this situation can occur
			if(polygon == null)
				return false;

			DrawPolyLine(polygon.points);

			if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path || !polygon.Refresh())
			{
				polygon.mesh.SetVertices(new Vector3[0]);
				polygon.mesh.SetFaces(new pb_Face[0]);
				polygon.mesh.SetSharedIndices(new pb_IntArray[0]);
				polygon.mesh.ToMesh();
				polygon.mesh.Refresh();
				pb_Editor.Refresh();

				return false;
			}

			if(pb_Editor.instance != null)
			{
				if(!vertexCountChanged)
					pb_Editor.instance.Internal_UpdateSelectionFast();
				else
					pb_Editor.Refresh();
			}

			return true;
		}

		void OnSceneGUI()
		{
			if(polygon == null || (polygon.polyEditMode == pb_PolyShape.PolyEditMode.None) || Tools.current != Tool.None)
			{
				if(polygon.polyEditMode != pb_PolyShape.PolyEditMode.None)
				{
					polygon.polyEditMode = pb_PolyShape.PolyEditMode.None;
				}

				return;
			}

			if(m_LineMaterial != null)
			{
				m_LineMaterial.SetPass(0);
				Graphics.DrawMeshNow(m_LineMesh, polygon.transform.localToWorldMatrix, 0);
			}

			Event evt = Event.current;

			// used when finishing a loop by clicking the first created point
			if(m_NextMouseUpAdvancesMode && evt.type == EventType.MouseUp)
			{
				evt.Use();
				m_NextMouseUpAdvancesMode = false;

				if( SceneCameraIsAlignedWithPolyUp() )
					SetPolyEditMode(pb_PolyShape.PolyEditMode.Edit);
				else
					SetPolyEditMode(pb_PolyShape.PolyEditMode.Height);
			}

			DoExistingPointsGUI();

			if(evt.type == EventType.KeyDown)
				HandleKeyEvent(evt.keyCode, evt.modifiers);

			if( pb_Handle_Utility.SceneViewInUse(evt) )
				return;

			int controlID = GUIUtility.GetControlID(FocusType.Passive);

			HandleUtility.AddDefaultControl(controlID);

			DoPointPlacement( HandleUtility.GUIPointToWorldRay(evt.mousePosition) );
		}

		void DoPointPlacement(Ray ray)
		{
			Event evt = Event.current;
			EventType eventType = evt.type;

			if(m_PlacingPoint)
			{
				if(	eventType == EventType.MouseDrag )
				{
					float hitDistance = Mathf.Infinity;
					m_Plane.SetNormalAndPosition(polygon.transform.up, polygon.transform.position);

					if( m_Plane.Raycast(ray, out hitDistance) )
					{
						evt.Use();
						polygon.points[m_SelectedIndex] = Snap(polygon.transform.InverseTransformPoint(ray.GetPoint(hitDistance)));
						UpdateMesh();
						SceneView.RepaintAll();
					}
				}

				if( eventType == EventType.MouseUp ||
					eventType == EventType.Ignore ||
					eventType == EventType.KeyDown ||
					eventType == EventType.KeyUp )
				{
					m_PlacingPoint = false;
					m_SelectedIndex = -1;
					SceneView.RepaintAll();
				}
			}
			else if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path)
			{
				if( eventType == EventType.MouseDown )
				{
					if(polygon.points.Count < 1)
						SetPlane(evt.mousePosition);

					float hitDistance = Mathf.Infinity;

					m_Plane.SetNormalAndPosition(polygon.transform.up, polygon.transform.position);

					if( m_Plane.Raycast(ray, out hitDistance) )
					{
						evt.Use();
						pbUndo.RecordObject(polygon, "Add Polygon Shape Point");
						polygon.points.Add(Snap(polygon.transform.InverseTransformPoint(ray.GetPoint(hitDistance))));
						m_PlacingPoint = true;
						m_SelectedIndex = polygon.points.Count - 1;
						UpdateMesh();
					}
				}
			}
			else if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Edit)
			{
				if(polygon.points.Count < 3)
				{
					SetPolyEditMode(pb_PolyShape.PolyEditMode.Path);
					return;
				}

				if(m_DistanceFromHeightHandle > 20f)
				{
					// point insertion
					int index;
					float distanceToLine;

					Vector3 p = pb_Handle_Utility.ClosestPointToPolyLine(polygon.points, out index, out distanceToLine, true, polygon.transform);
					Vector3 wp = polygon.transform.TransformPoint(p);

					Vector2 ga = HandleUtility.WorldToGUIPoint(polygon.transform.TransformPoint(polygon.points[index % polygon.points.Count]));
					Vector2 gb = HandleUtility.WorldToGUIPoint(polygon.transform.TransformPoint(polygon.points[(index - 1)]));

					Vector2 mouse = evt.mousePosition;

					float distanceToVertex = Mathf.Min(Vector2.Distance(mouse, ga), Vector2.Distance(mouse, gb));

					if(distanceToVertex > 20f && distanceToLine < 20f)
					{
						Handles.color = Color.green;

						Handles.DotCap(-1, wp, Quaternion.identity, HandleUtility.GetHandleSize(wp) * .05f);

						if( evt.type == EventType.MouseDown )
						{
							evt.Use();

							pbUndo.RecordObject(polygon, "Insert Point");
							polygon.points.Insert(index, p);
							m_SelectedIndex = index;
							m_PlacingPoint = true;
							UpdateMesh(true);
						}

						Handles.color = Color.white;
					}
				}
			}
		}

		void DoExistingPointsGUI()
		{
			Transform trs = polygon.transform;
			int len = polygon.points.Count;
			Vector3 up = polygon.transform.up;
			Vector3 right = polygon.transform.right;
			Vector3 forward = polygon.transform.forward;
			Vector3 center = Vector3.zero;

			Event evt = Event.current;

			bool used = evt.type == EventType.Used;

			if(!used && 
				(	evt.type == EventType.MouseDown &&
					evt.button == 0 &&
					!IsAppendModifier(evt.modifiers)
				)
			)
			{
				m_SelectedIndex = -1;
				Repaint();
			}

			if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Height)
			{
				if(!used && evt.type == EventType.MouseUp && evt.button == 0 && !IsAppendModifier(evt.modifiers))
				{
					SetPolyEditMode(pb_PolyShape.PolyEditMode.Edit);
				}
				
				bool sceneInUse = pb_Handle_Utility.SceneViewInUse(evt);
				Ray r = HandleUtility.GUIPointToWorldRay(evt.mousePosition);

				Vector3 origin = polygon.transform.TransformPoint(pb_Math.Average(polygon.points));

				float extrude = polygon.extrude;
				
				if(!sceneInUse)
				{
					Vector3 p = pb_Math.GetNearestPointRayRay(origin, trs.up, r.origin, r.direction);
					extrude = Snap(Vector3.Distance(origin, p) * Mathf.Sign(Vector3.Dot(p-origin, up)));
				}

				Vector3 extrudePoint = origin + (extrude * up);

				Handles.color = HANDLE_COLOR;
				Handles.DotCap(-1, origin, Quaternion.identity, HandleUtility.GetHandleSize(origin) * .05f);
				Handles.color = HANDLE_GREEN;
				Handles.DrawLine(origin, extrudePoint);
				Handles.DotCap(-1, extrudePoint, Quaternion.identity, HandleUtility.GetHandleSize(extrudePoint) * .05f);
				Handles.color = Color.white;
				
				
				if( !sceneInUse && polygon.extrude != extrude)
				{
					polygon.extrude = extrude;
					UpdateMesh();
				}
			}
			else
			{
				// vertex dots
				for(int ii = 0; ii < len; ii++)
				{
					Vector3 point = trs.TransformPoint(polygon.points[ii]);

					center.x += point.x;
					center.y += point.y;
					center.z += point.z;

					float size = HandleUtility.GetHandleSize(point) * .05f;

					Handles.color = ii == m_SelectedIndex ? SELECTED_COLOR : HANDLE_COLOR;

					EditorGUI.BeginChangeCheck();

					point = Handles.Slider2D(point, up, right, forward, size, Handles.DotCap, Vector2.zero, true);

					if(EditorGUI.EndChangeCheck())
					{
						pbUndo.RecordObject(polygon, "Move Polygon Shape Point");
						polygon.points[ii] = Snap(trs.InverseTransformPoint(point));
						UpdateMesh(true);
					}

					// "clicked" a button
					if( !used && evt.type == EventType.Used )
					{
						if(ii == 0 && polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path)
						{
							m_NextMouseUpAdvancesMode = true;
							return;
						}
						else
						{
							used = true;
							m_SelectedIndex = ii;
						}
					}
				}

				Handles.color = Color.white;

				// height setting
				if(polygon.polyEditMode != pb_PolyShape.PolyEditMode.Path && polygon.points.Count > 2)
				{
					center.x /= (float) len;
					center.y /= (float) len;
					center.z /= (float) len;

					Vector3 extrude = center + (up * polygon.extrude);
					m_DistanceFromHeightHandle = Vector2.Distance(HandleUtility.WorldToGUIPoint(extrude), evt.mousePosition);

					EditorGUI.BeginChangeCheck();

					Handles.color = HANDLE_COLOR;
					Handles.DotCap(-1, center, Quaternion.identity, HandleUtility.GetHandleSize(center) * .05f);
					Handles.DrawLine(center, extrude);
					Handles.color = HANDLE_GREEN;
					extrude = Handles.Slider(extrude, up, HandleUtility.GetHandleSize(extrude) * .05f, Handles.DotCap, 0f);
					Handles.color = Color.white;

					if(EditorGUI.EndChangeCheck())
					{
						pbUndo.RecordObject(polygon, "Set Polygon Shape Height");
						polygon.extrude = Snap(Vector3.Distance(extrude, center) * Mathf.Sign(Vector3.Dot(up, extrude - center)));
						UpdateMesh(false);
					}
				}
			}
		}

		bool IsAppendModifier(EventModifiers em)
		{
			return 	(em & EventModifiers.Shift) == EventModifiers.Shift ||
					(em & EventModifiers.Control) == EventModifiers.Control ||
					(em & EventModifiers.Alt) == EventModifiers.Alt ||
					(em & EventModifiers.Command) == EventModifiers.Command;
		}

		void HandleKeyEvent(KeyCode key, EventModifiers modifier)
		{
			switch(key)
			{
				case KeyCode.Space:
				case KeyCode.Return:
				{
					if( polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path )
					{
						if( SceneCameraIsAlignedWithPolyUp() )
							SetPolyEditMode(pb_PolyShape.PolyEditMode.Edit);
						else
							SetPolyEditMode(pb_PolyShape.PolyEditMode.Height);
					}
					else if( polygon.polyEditMode == pb_PolyShape.PolyEditMode.Height )
						SetPolyEditMode(pb_PolyShape.PolyEditMode.Edit);
					else if( polygon.polyEditMode == pb_PolyShape.PolyEditMode.Edit )
						SetPolyEditMode(pb_PolyShape.PolyEditMode.None);

					break;
				}

				case KeyCode.Backspace:
				{
					if(m_SelectedIndex > -1)
					{
						pbUndo.RecordObject(polygon, "Delete Selected Points");
						polygon.points.RemoveAt(m_SelectedIndex);
						m_SelectedIndex = -1;
						UpdateMesh();
					}
					break;
				}

				case KeyCode.Escape:
				{
					if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path || polygon.polyEditMode == pb_PolyShape.PolyEditMode.Height)
					{
						Undo.DestroyObjectImmediate(polygon.gameObject);						
					}
					else if(polygon.polyEditMode == pb_PolyShape.PolyEditMode.Edit)
					{
						SetPolyEditMode(pb_PolyShape.PolyEditMode.None);
					}

					break;
				}
			}
		}

		void DrawPolyLine(List<Vector3> points)
		{
			if(points.Count < 2)
				return;

			int vc = polygon.polyEditMode == pb_PolyShape.PolyEditMode.Path ? points.Count : points.Count + 1;

			Vector3[] ver = new Vector3[vc];
			Vector2[] uvs = new Vector2[vc];
			int[] indices = new int[vc];
			int cnt = points.Count;
			float distance = 0f;

			for(int i = 0; i < vc; i++)
			{
				Vector3 a = points[i % cnt];
				Vector3 b = points[i < 1 ? 0 : i - 1];

				float d = Vector3.Distance(a, b);
				distance += d;

				ver[i] = points[i % cnt];
				uvs[i] = new Vector2(distance, 1f);
				indices[i] = i;
			}

			m_LineMesh.Clear();
			m_LineMesh.name = "Poly Shape Guide";
			m_LineMesh.vertices = ver;
			m_LineMesh.uv = uvs;
			m_LineMesh.SetIndices(indices, MeshTopology.LineStrip, 0);
			m_LineMaterial.SetFloat("_LineDistance", distance);
		}

		Vector3 Snap(Vector3 point)
		{
			if(pb_ProGrids_Interface.SnapEnabled())
			{
				float snap = pb_ProGrids_Interface.SnapValue();
				return pbUtil.SnapValue(point, new Vector3(snap, 0f, snap));
			}

			return point;
		}

		float Snap(float point)
		{
			if(pb_ProGrids_Interface.SnapEnabled())
				return pbUtil.SnapValue(point, pb_ProGrids_Interface.SnapValue());
			return point;
		}

		/**
		 *	Is the scene camera looking directly at the up vector of the current polygon?
		 *	Prevents a situation where the height tool is rendered useless by coplanar
		 *	ray tracking.
		 */
		bool SceneCameraIsAlignedWithPolyUp()
		{
			float dot = Vector3.Dot(SceneView.lastActiveSceneView.camera.transform.forward, polygon.transform.up);
			return Mathf.Abs(Mathf.Abs(dot) - 1f) < .01f;
		}

		void OnEditLevelChange(int editLevel)
		{
			if( polygon.polyEditMode != pb_PolyShape.PolyEditMode.None && ((EditLevel)editLevel) != EditLevel.Plugin)
				polygon.polyEditMode = pb_PolyShape.PolyEditMode.None;
		}

		void UndoRedoPerformed()
		{
			if(m_LineMesh != null)
				GameObject.DestroyImmediate(m_LineMesh);

			m_LineMesh = new Mesh();

			if(polygon.polyEditMode != pb_PolyShape.PolyEditMode.None)
				UpdateMesh(true);
		}
	}
}