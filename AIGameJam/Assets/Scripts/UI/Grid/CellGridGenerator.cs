using System;
using System.Collections.Generic;
using Nova;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class CellGridGenerator : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedCells";

    [Header("References")]
    [SerializeField] private GridManager gridManager = null;
    [SerializeField] private Transform cellParent = null;
    [SerializeField] private ItemView cellPrefab = null;

    [Header("Generation")]
    [SerializeField] private bool centerGridOnParent = true;
    [Min(1)] [SerializeField] private int columns = 8;
    [Min(1)] [SerializeField] private int rows = 6;
    [SerializeField] private Vector2 spacing = Vector2.one;
    [SerializeField] private Vector2 localOffset = Vector2.zero;

    [Header("Excluded Cells")]
    [SerializeField, HideInInspector] private List<Vector2Int> excludedCells = new();

    [SerializeField, HideInInspector] private Transform generatedCellRoot = null;

    private void Reset()
    {
        cellParent = transform;
        gridManager = ResolveGridManager();
    }

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RebuildCells();
    }

    private void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        spacing.x = Mathf.Max(0.01f, spacing.x);
        spacing.y = Mathf.Max(0.01f, spacing.y);

        if (cellParent == null)
        {
            cellParent = transform;
        }

        if (gridManager == null)
        {
            gridManager = ResolveGridManager();
        }
    }

    [ContextMenu("Rebuild Cells")]
    public void RebuildCells()
    {
        if (cellPrefab == null)
        {
            Debug.LogWarning("CellGridGenerator needs a cell prefab before it can build the grid.", this);
            return;
        }

        Transform root = EnsureGeneratedCellRoot(true);
        ClearChildren(root);

        Vector2 startOffset = GetStartOffset();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector2Int cellIndex = new(column, row);
                if (IsCellExcluded(cellIndex))
                {
                    continue;
                }

                GameObject cellInstance = CreateCellInstance(root);
                cellInstance.name = $"{cellPrefab.name}_{column}_{row}";

                ApplyCellPosition(cellInstance, new Vector3(
                    startOffset.x + (column * spacing.x),
                    startOffset.y + (row * spacing.y),
                    0f));
            }
        }

        RefreshGridManager();
    }

    [ContextMenu("Clear Generated Cells")]
    public void ClearGeneratedCells()
    {
        Transform root = EnsureGeneratedCellRoot(false);
        if (root == null)
        {
            RefreshGridManager();
            return;
        }

        ClearChildren(root);
        RefreshGridManager();
    }

    [ContextMenu("Clear Excluded Cells")]
    public void ClearExcludedCells()
    {
        excludedCells.Clear();
    }

    public bool IsCellExcluded(Vector2Int cellIndex)
    {
        return excludedCells.Contains(cellIndex);
    }

    public bool SetCellExcluded(Vector2Int cellIndex, bool excluded)
    {
        if (excluded)
        {
            if (excludedCells.Contains(cellIndex))
            {
                return false;
            }

            excludedCells.Add(cellIndex);
            SortExcludedCells();
            return true;
        }

        bool removed = excludedCells.RemoveAll(cell => cell == cellIndex) > 0;
        if (removed)
        {
            SortExcludedCells();
        }

        return removed;
    }

    private GridManager ResolveGridManager()
    {
        return gridManager != null ? gridManager : GetComponent<GridManager>() ?? GetComponentInParent<GridManager>();
    }

    private void SortExcludedCells()
    {
        excludedCells.Sort((left, right) =>
        {
            int rowCompare = left.y.CompareTo(right.y);
            return rowCompare != 0 ? rowCompare : left.x.CompareTo(right.x);
        });
    }

    private Transform EnsureGeneratedCellRoot(bool createIfMissing)
    {
        Transform parent = cellParent != null ? cellParent : transform;

        if (generatedCellRoot == null)
        {
            Transform existingRoot = parent.Find(GeneratedRootName);
            if (existingRoot != null)
            {
                generatedCellRoot = existingRoot;
            }
        }

        if (generatedCellRoot == null && !createIfMissing)
        {
            return null;
        }

        if (generatedCellRoot == null)
        {
            GameObject rootObject = new(GeneratedRootName);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Generated Cells Root");
            }
#endif
            generatedCellRoot = rootObject.transform;
        }

        // Nova gesture routing only reaches descendants through a connected UIBlock hierarchy.
        // The generated grouping node must therefore be a UIBlock as well, not a plain Transform.
        if (generatedCellRoot.GetComponent<UIBlock>() == null)
        {
            generatedCellRoot.gameObject.AddComponent<UIBlock>();
        }

        generatedCellRoot.SetParent(parent, false);
        generatedCellRoot.localPosition = Vector3.zero;
        generatedCellRoot.localRotation = Quaternion.identity;
        generatedCellRoot.localScale = Vector3.one;
        return generatedCellRoot;
    }

    private Vector2 GetStartOffset()
    {
        if (!centerGridOnParent)
        {
            return localOffset;
        }

        return localOffset + new Vector2(
            -0.5f * (columns - 1) * spacing.x,
            -0.5f * (rows - 1) * spacing.y);
    }

    private void RefreshGridManager()
    {
        gridManager = ResolveGridManager();
        if (gridManager != null)
        {
            if (cellParent != null &&
                cellParent != gridManager.transform &&
                !cellParent.IsChildOf(gridManager.transform))
            {
                Debug.LogWarning(
                    "CellGridGenerator should place cells under the GridManager object or one of its children. Nova gesture events only work through a connected UIBlock hierarchy, so a plain Transform between the root UIBlock and the cells will block interaction.",
                    this);
            }

            gridManager.CollectCells();
        }
    }

    private GameObject CreateCellInstance(Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab.gameObject);
            Undo.RegisterCreatedObjectUndo(instance, "Generate Cells");
            instance.transform.SetParent(parent, false);
            return instance;
        }
#endif

        return Instantiate(cellPrefab.gameObject, parent, false);
    }

    private static void ApplyCellPosition(GameObject cellInstance, Vector3 localPosition)
    {
        if (cellInstance == null)
        {
            return;
        }

        UIBlock2D uiBlock = cellInstance.GetComponent<UIBlock2D>();
        if (uiBlock == null)
        {
            cellInstance.transform.localPosition = localPosition;
            cellInstance.transform.localRotation = Quaternion.identity;
            cellInstance.transform.localScale = Vector3.one;
            return;
        }

        uiBlock.Position.X = Length.FixedValue(localPosition.x);
        uiBlock.Position.Y = Length.FixedValue(localPosition.y);
        uiBlock.Position.Z = Length.FixedValue(localPosition.z);
        cellInstance.transform.localRotation = Quaternion.identity;
        cellInstance.transform.localScale = Vector3.one;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(child);
                continue;
            }
#endif
            Destroy(child);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform previewRoot = cellParent != null ? cellParent : transform;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = previewRoot.localToWorldMatrix;

        Vector2 startOffset = GetStartOffset();
        Vector3 previewSize = new(
            Mathf.Max(0.1f, spacing.x * 0.9f),
            Mathf.Max(0.1f, spacing.y * 0.9f),
            0.01f);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector2Int cellIndex = new(column, row);
                Vector3 localPosition = new(
                    startOffset.x + (column * spacing.x),
                    startOffset.y + (row * spacing.y),
                    0f);

                Gizmos.color = IsCellExcluded(cellIndex)
                    ? new Color(0.9f, 0.25f, 0.25f, 0.9f)
                    : new Color(0.2f, 0.8f, 0.35f, 0.9f);
                Gizmos.DrawWireCube(localPosition, previewSize);
            }
        }

        Gizmos.matrix = previousMatrix;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CellGridGenerator))]
public class CellGridGeneratorEditor : Editor
{
    private SerializedProperty columnsProperty;
    private SerializedProperty rowsProperty;

    private void OnEnable()
    {
        columnsProperty = serializedObject.FindProperty("columns");
        rowsProperty = serializedObject.FindProperty("rows");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "excludedCells");

        EditorGUILayout.Space(8f);
        DrawExcludedCellGrid();
        EditorGUILayout.Space(8f);
        DrawActionButtons();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawExcludedCellGrid()
    {
        CellGridGenerator generator = (CellGridGenerator)target;
        if (generator == null)
        {
            return;
        }

        int columns = columnsProperty != null ? Mathf.Max(1, columnsProperty.intValue) : 1;
        int rows = rowsProperty != null ? Mathf.Max(1, rowsProperty.intValue) : 1;

        EditorGUILayout.LabelField("Excluded Cells", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click a cell to toggle it off. Red cells are excluded from generation. Use Rebuild Cells to refresh the spawned preview. Row 0 is the bottom row.",
            MessageType.Info);

        float availableWidth = Mathf.Max(0f, EditorGUIUtility.currentViewWidth - 54f);
        float cellSize = Mathf.Clamp((availableWidth - 24f) / Mathf.Max(1, columns), 18f, 28f);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(24f);
        for (int column = 0; column < columns; column++)
        {
            GUILayout.Label(
                column.ToString(),
                EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(cellSize),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
        EditorGUILayout.EndHorizontal();

        for (int row = rows - 1; row >= 0; row--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                row.ToString(),
                EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(24f),
                GUILayout.Height(cellSize));

            for (int column = 0; column < columns; column++)
            {
                Vector2Int cellIndex = new(column, row);
                bool excluded = generator.IsCellExcluded(cellIndex);
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = excluded
                    ? new Color(0.85f, 0.3f, 0.3f, 1f)
                    : new Color(0.25f, 0.7f, 0.35f, 1f);

                bool newExcluded = GUILayout.Toggle(
                    excluded,
                    GUIContent.none,
                    EditorStyles.miniButton,
                    GUILayout.Width(cellSize),
                    GUILayout.Height(cellSize));

                GUI.backgroundColor = previousBackground;

                if (newExcluded != excluded)
                {
                    Undo.RecordObject(generator, newExcluded ? "Exclude Cell" : "Include Cell");
                    generator.SetCellExcluded(cellIndex, newExcluded);
                    EditorUtility.SetDirty(generator);

                    if (!Application.isPlaying)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(generator);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawActionButtons()
    {
        CellGridGenerator generator = (CellGridGenerator)target;
        if (generator == null)
        {
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Rebuild Cells"))
        {
            generator.RebuildCells();
        }

        if (GUILayout.Button("Clear Generated Cells"))
        {
            generator.ClearGeneratedCells();
        }

        if (GUILayout.Button("Clear Exclusions"))
        {
            Undo.RecordObject(generator, "Clear Cell Exclusions");
            generator.ClearExcludedCells();
            EditorUtility.SetDirty(generator);

            if (!Application.isPlaying)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(generator);
            }
        }

        EditorGUILayout.EndHorizontal();
    }
}
#endif
