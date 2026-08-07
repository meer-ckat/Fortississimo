using System.Collections.Generic;
using UnityEngine;

public class JsonParser : MonoBehaviour
{
    public const string MapFolder = "Data/Map";

    // 파일명(확장자 제외) -> 맵 데이터
    private static Dictionary<string, List<Cell>> maps;
    private static List<string> mapNames;

    // 씬에 JsonParser 오브젝트가 없어도, Awake 순서가 어떻든 동작하도록 지연 로드로 둔다
    public static IReadOnlyList<string> MapNames
    {
        get
        {
            EnsureLoaded();
            return mapNames;
        }
    }

    private void Awake()
    {
        EnsureLoaded();
    }

    public static bool TryGetMap(string mapName, out List<Cell> cells)
    {
        EnsureLoaded();

        if(!string.IsNullOrEmpty(mapName) && maps.TryGetValue(mapName, out var found))
        {
            // 호출한 쪽에서 리스트를 건드려도 캐시가 오염되지 않게 복사본을 넘긴다
            cells = new List<Cell>(found);
            return true;
        }

        cells = null;
        return false;
    }

    public static List<Cell> GetMap(string mapName)
    {
        if(TryGetMap(mapName, out var cells))
            return cells;

        Debug.LogError($"맵 \"{mapName}\"을 찾을 수 없음. Resources/{MapFolder} 안에 {mapName}.json이 있는지 확인.");
        return new List<Cell>();
    }

    // 맵 파일을 추가/수정한 뒤 다시 읽고 싶을 때
    public static void Reload()
    {
        maps = null;
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if(maps != null)
            return;

        maps = new Dictionary<string, List<Cell>>();
        mapNames = new List<string>();

        // .json은 Unity가 TextAsset으로 임포트하므로 폴더째로 읽힌다
        TextAsset[] files = Resources.LoadAll<TextAsset>(MapFolder);

        foreach(var file in files)
        {
            var cells = Parse(file);
            if(cells == null)
                continue;

            maps[file.name] = cells;
            mapNames.Add(file.name);
        }

        Debug.Log($"맵 데이터 {maps.Count}개 로드됨: {string.Join(", ", mapNames)}");
    }

    private static List<Cell> Parse(TextAsset file)
    {
        MapJson json;
        try
        {
            json = JsonUtility.FromJson<MapJson>(file.text);
        }
        catch(System.Exception e)
        {
            Debug.LogError($"[{file.name}] JSON 파싱 실패: {e.Message}");
            return null;
        }

        if(json == null || json.cells == null || json.cells.Count == 0)
        {
            Debug.LogError($"[{file.name}] cells가 비어있음. 루트가 {{\"cells\":[...]}} 형태인지 확인.");
            return null;
        }

        var cells = new List<Cell>(json.cells.Count);
        foreach(var cellJson in json.cells)
            cells.Add(cellJson.ToCell(file.name));

        return cells;
    }
}
