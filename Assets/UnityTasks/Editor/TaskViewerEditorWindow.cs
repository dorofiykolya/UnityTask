using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityTask
{
    public class TaskViewerEditorWindow : EditorWindow
    {
        [MenuItem("Window/Tools/Task Viewer")]
        public static void Open()
        {
            GetWindow<TaskViewerEditorWindow>("◄►TaskView").Show(true);
        }

        private Vector2 _scrollView;

        public void OnGUI()
        {
            _scrollView = EditorGUILayout.BeginScrollView(_scrollView);
            var index = 0;
            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Label("#", "box", GUILayout.Width(20f));
            GUILayout.Label("Id", "box", GUILayout.Width(30f));
            GUILayout.Label("State", "box", GUILayout.Width(150f));
            GUILayout.Label("CurrentThreadExecute", "box", GUILayout.Width(150f));
            GUILayout.Label("NextThreadExecute", "box", GUILayout.Width(150f));
            GUILayout.Label("LifeTime", "box", GUILayout.Width(150f));
            GUILayout.Label("LastState", "box", GUILayout.Width(200f));
            GUILayout.Label("Ticks", "box", GUILayout.Width(50f));
            EditorGUILayout.EndHorizontal();
            foreach (var task in Task.Tasks)
            {
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label(index.ToString(), "box", GUILayout.Width(20f));
                GUILayout.Label(task.Id.ToString(), "box", GUILayout.Width(30f));
                GUILayout.Label(task.State.ToString(), "box", GUILayout.Width(150f));
                GUILayout.Label(task.CurrentThreadExecute.ToString(), "box", GUILayout.Width(150f));
                GUILayout.Label(task.NextThreadExecute.ToString(), "box", GUILayout.Width(150f));
                GUILayout.Label(TimeSpan.FromMilliseconds(task.LifeTime).ToString(), "box", GUILayout.Width(150f));
                GUILayout.Label(Convert.ToString(task.LastState), "box", GUILayout.Width(200f));
                GUILayout.Label(Convert.ToString(task.Ticks), "box", GUILayout.Width(50f));
                EditorGUILayout.EndHorizontal();
                index++;
            }
            EditorGUILayout.EndScrollView();
        }

        public void OnDisable()
        {
            if (EditorApplication.update != null)
            {
                EditorApplication.update -= Repaint;
            }
        }

        public void OnEnable()
        {
            EditorApplication.update += Repaint;
        }
    }
}
