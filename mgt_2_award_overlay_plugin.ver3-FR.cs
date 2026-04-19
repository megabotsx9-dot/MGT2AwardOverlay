// ver3
// dotnet build .\MGT2AwardOverlay.csproj
// FR

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System.Text;

namespace MGT2AwardOverlay
{
    [BepInPlugin("com.waniwani.mgt2.awardoverlay", "MGT2 Award Overlay", "1.3.0")]
    public sealed class AwardOverlayPlugin : BaseUnityPlugin
    {
        private readonly List<CompanyScoreRow> _studioAwardRows = new List<CompanyScoreRow>();
        private readonly List<CompanyScoreRow> _publisherAwardRows = new List<CompanyScoreRow>();

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<KeyCode> _devToggleKey;
        private ConfigEntry<KeyCode> _awardToggleKey;
        private ConfigEntry<int> _fontSize;
        private ConfigEntry<int> _devWindowX;
        private ConfigEntry<int> _devWindowY;
        private ConfigEntry<int> _devWindowW;
        private ConfigEntry<int> _devWindowH;
        private ConfigEntry<int> _awardWindowX;
        private ConfigEntry<int> _awardWindowY;
        private ConfigEntry<int> _awardWindowW;
        private ConfigEntry<int> _awardWindowH;

        private bool _devVisible = true;
        private bool _awardVisible = true;
        private bool _compact = false;

        private Rect _devWindowRect;
        private Rect _awardWindowRect;
        private Vector2 _devScroll;
        private Vector2 _awardScroll;

        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;

        private object _mainScript;
        private GameObject _resolvedMainGameObject;

        private string _devStatus = "En attente de l'initialisation...";
        private string _awardStatus = "En attente de l'initialisation...";
        private string _devDebugInfo = "";
        private string _awardDebugInfo = "";

        private DateTime _lastDevRefresh = DateTime.MinValue;
        private DateTime _lastAwardRefresh = DateTime.MinValue;

        private readonly List<DevTaskRow> _devRows = new List<DevTaskRow>();
        private readonly List<AwardGameRow> _gfxAwardRows = new List<AwardGameRow>();
        private readonly List<AwardGameRow> _soundAwardRows = new List<AwardGameRow>();
        private readonly List<AwardGameRow> _gotyRows = new List<AwardGameRow>();
        private readonly List<AwardGameRow> _worstRows = new List<AwardGameRow>();
        private readonly List<AwardGameRow> _selfRows = new List<AwardGameRow>();

        private string GetCompanyNameById(int companyId)
        {
            if (_mainScript == null || companyId <= 0)
                return "?";

            string myName = GetString(_mainScript, "myName");
            if (!string.IsNullOrEmpty(myName) && companyId == GetInt(_mainScript, "myID"))
                return myName;

            object[] rooms = GetArray(_mainScript, "arrayRoomScripts");
            if (rooms != null)
            {
                for (int i = 0; i < rooms.Length; i++)
                {
                    object room = rooms[i];
                    if (room == null)
                        continue;

                    int roomOwner = GetInt(room, "ownerID");
                    if (roomOwner != companyId)
                        continue;

                    string roomName = GetString(room, "firmenname");
                    if (!string.IsNullOrEmpty(roomName))
                        return roomName;

                    roomName = GetString(room, "companyName");
                    if (!string.IsNullOrEmpty(roomName))
                        return roomName;
                }
            }

            object companies = GetFieldOrPropertyValue(_mainScript, "companies_");
            if (companies != null)
            {
                object[] companyArray = GetArray(companies, "arrayCompanies");
                if (companyArray == null)
                    companyArray = GetArray(companies, "companies");

                if (companyArray != null)
                {
                    for (int i = 0; i < companyArray.Length; i++)
                    {
                        object c = companyArray[i];
                        if (c == null)
                            continue;

                        int id = GetInt(c, "myID");
                        if (id != companyId)
                            continue;

                        string name = GetString(c, "name");
                        if (!string.IsNullOrEmpty(name))
                            return name;

                        name = GetString(c, "firmenname");
                        if (!string.IsNullOrEmpty(name))
                            return name;

                        name = GetString(c, "companyName");
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }

            return "ID:" + companyId;
        }

        private string DebugReleaseFields(object g)
        {
            if (g == null) return "";

            Type t = g.GetType();
            StringBuilder sb = new StringBuilder();

            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                string n = fields[i].Name.ToLowerInvariant();
                if (n.Contains("release") || n.Contains("date") || n.Contains("year") || n.Contains("month") || n.Contains("week")
                    || n.Contains("jahr") || n.Contains("monat") || n.Contains("woche"))
                {
                    object v = null;
                    try { v = fields[i].GetValue(g); } catch { }
                    if (v != null)
                    {
                        if (sb.Length > 0) sb.Append(" | ");
                        sb.Append(fields[i].Name).Append("=").Append(v.ToString());
                    }
                }
            }

            PropertyInfo[] props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].GetIndexParameters().Length > 0)
                    continue;

                string n = props[i].Name.ToLowerInvariant();
                if (n.Contains("release") || n.Contains("date") || n.Contains("year") || n.Contains("month") || n.Contains("week")
                    || n.Contains("jahr") || n.Contains("monat") || n.Contains("woche"))
                {
                    object v = null;
                    try { v = props[i].GetValue(g, null); } catch { }
                    if (v != null)
                    {
                        if (sb.Length > 0) sb.Append(" | ");
                        sb.Append(props[i].Name).Append("=").Append(v.ToString());
                    }
                }
            }

            return sb.ToString();
        }

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "Activer / Désactiver");
            _devToggleKey = Config.Bind("General", "DevToggleKey", KeyCode.F7, "F7 Basculer la prévision de développement");
            _awardToggleKey = Config.Bind("General", "AwardToggleKey", KeyCode.F8, "F8 Basculer les récompenses");
            _fontSize = Config.Bind("UI", "FontSize", 14, "Taille de police");

            _devWindowX = Config.Bind("UI", "DevWindowX", 20, "Fenêtre dev X");
            _devWindowY = Config.Bind("UI", "DevWindowY", 20, "Fenêtre dev Y");
            _devWindowW = Config.Bind("UI", "DevWindowW", 780, "Largeur fenêtre dev");
            _devWindowH = Config.Bind("UI", "DevWindowH", 920, "Hauteur fenêtre dev");

            _awardWindowX = Config.Bind("UI", "AwardWindowX", 830, "Fenêtre récompenses X");
            _awardWindowY = Config.Bind("UI", "AwardWindowY", 20, "Fenêtre récompenses Y");
            _awardWindowW = Config.Bind("UI", "AwardWindowW", 760, "Largeur fenêtre récompenses");
            _awardWindowH = Config.Bind("UI", "AwardWindowH", 920, "Hauteur fenêtre récompenses");

            _devWindowRect = new Rect(_devWindowX.Value, _devWindowY.Value, _devWindowW.Value, _devWindowH.Value);
            _awardWindowRect = new Rect(_awardWindowX.Value, _awardWindowY.Value, _awardWindowW.Value, _awardWindowH.Value);

            Logger.LogInfo("MGT2 Award Overlay loaded.");
        }

        private void Update()
        {
            if (!_enabled.Value)
                return;

            if (UnityEngine.Input.GetKeyDown(_devToggleKey.Value))
                _devVisible = !_devVisible;

            if (UnityEngine.Input.GetKeyDown(_awardToggleKey.Value))
                _awardVisible = !_awardVisible;

            if ((_devVisible || _awardVisible) && UnityEngine.Input.GetKeyDown(KeyCode.F6))
                _compact = !_compact;

            if (NeedsReResolve())
                ResetResolvedReferences();

            TryResolveMainScript();

            if ((DateTime.UtcNow - _lastDevRefresh).TotalSeconds >= 1.0)
            {
                RefreshDevRows();
                _lastDevRefresh = DateTime.UtcNow;
            }

            if ((DateTime.UtcNow - _lastAwardRefresh).TotalSeconds >= 2.0)
            {
                RefreshAwardRows();
                _lastAwardRefresh = DateTime.UtcNow;
            }
        }

        private void OnGUI()
        {
            if (!_enabled.Value)
                return;

            EnsureStyle();

            if (_devVisible)
                _devWindowRect = GUI.Window(879323, _devWindowRect, DrawDevWindow, "MGT2 Dev Debug");

            if (_awardVisible)
                _awardWindowRect = GUI.Window(879324, _awardWindowRect, DrawAwardWindow, "MGT2 Awards Overlay");
        }

        private void DrawDevWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("=== Débogage du développement ===", _headerStyle);
            GUILayout.Label("Statut : " + _devStatus, _labelStyle);

            if (!string.IsNullOrEmpty(_devDebugInfo))
                GUILayout.Label(_devDebugInfo, _labelStyle);

            if (_mainScript == null)
            {
                GUILayout.Label("mainScript non résolu", _labelStyle);
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0, 0, 10000, 24));
                return;
            }

            string mainName = "?";
            if (_resolvedMainGameObject != null)
                mainName = _resolvedMainGameObject.name;

            int myId = GetInt(_mainScript, "myID");
            int year = GetInt(_mainScript, "year");
            int month = GetInt(_mainScript, "month");
            int week = GetInt(_mainScript, "week");

            object[] rooms = GetArray(_mainScript, "arrayRoomScripts");

            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("main=" + mainName, _labelStyle);
            GUILayout.Label("myID=" + myId + "  date=" + year + "/" + month + "/" + week, _labelStyle);
            GUILayout.Label("arrayRoomScripts=" + (rooms != null ? rooms.Length.ToString() : "null") +
                            "  |  F6 compact=" + (_compact ? "ON" : "OFF"), _labelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(8);
            _devScroll = GUILayout.BeginScrollView(_devScroll, GUILayout.ExpandHeight(true));

            if (_devRows.Count == 0)
            {
                GUILayout.Label("<color=yellow>Aucune entrée à afficher</color>", _labelStyle);
            }
            else
            {
                for (int i = 0; i < _devRows.Count; i++)
                    DrawDevRow(_devRows[i]);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private void DrawAwardWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("=== Superposition des récompenses ===", _headerStyle);
            GUILayout.Label("Statut : " + _awardStatus, _labelStyle);

            if (!string.IsNullOrEmpty(_awardDebugInfo))
                GUILayout.Label(_awardDebugInfo, _labelStyle);

            if (_mainScript == null)
            {
                GUILayout.Label("mainScript non résolu", _labelStyle);
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0, 0, 10000, 24));
                return;
            }

            int myId = GetInt(_mainScript, "myID");
            int year = GetInt(_mainScript, "year");
            int month = GetInt(_mainScript, "month");
            int week = GetInt(_mainScript, "week");

            int displayStartYear;
            if (month >= 12)
                displayStartYear = year;
            else
                displayStartYear = year - 1;

            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("myID=" + myId + "  date=" + year + "/" + month + "/" + week, _labelStyle);
            GUILayout.Label("Période : " + displayStartYear + "/12/1 - " + year + "/" + month + "/" + week, _labelStyle);
            GUILayout.Label("F6 compact=" + (_compact ? "ON" : "OFF"), _labelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(8);
            _awardScroll = GUILayout.BeginScrollView(_awardScroll, GUILayout.ExpandHeight(true));

            DrawAwardSection("Candidats au meilleur graphisme", _gfxAwardRows, "GFX");
            DrawAwardSection("Candidats au meilleur son", _soundAwardRows, "SND");
            DrawAwardSection("Candidats au jeu de l'année", _gotyRows, "TOTAL");
            DrawAwardSection("Candidats au pire jeu", _worstRows, "LOW");
            DrawCompanySection("Candidats au meilleur développeur", _studioAwardRows, "STUDIO");
            DrawCompanySection("Candidats au meilleur éditeur", _publisherAwardRows, "PUBLISHER");
            DrawAwardSection("Jeux sortis de votre société", _selfRows, "SELF");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private void DrawAwardSection(string title, List<AwardGameRow> rows, string mode)
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("=== " + title + " ===", _headerStyle);

            if (rows.Count == 0)
            {
                GUILayout.Label("(aucun)", _labelStyle);
                GUILayout.EndVertical();
                GUILayout.Space(6);
                return;
            }

            int limit = _compact ? 5 : 10;

            for (int i = 0; i < rows.Count && i < limit; i++)
            {
                AwardGameRow r = rows[i];

                string rel = r.ReleaseYear > 0
                    ? (" | rel=" + r.ReleaseYear + "/" + r.ReleaseMonth + "/" + r.ReleaseWeek)
                    : " | rel=?";

                string line;

                if (mode == "GFX")
                {
                    line = (i + 1) + ". " + r.Name
                        + " | owner=" + r.OwnerName + " [" + r.OwnerId + "]"
                        + " | dev=" + r.DeveloperName + " [" + r.DeveloperId + "]"
                        + " | pub=" + r.PublisherName + " [" + r.PublisherId + "]"
                        + " | GFX " + r.ReviewGrafik
                        + rel;
                }
                else if (mode == "SND")
                {
                    line = (i + 1) + ". " + r.Name
                        + " | owner=" + r.OwnerName + " [" + r.OwnerId + "]"
                        + " | dev=" + r.DeveloperName + " [" + r.DeveloperId + "]"
                        + " | pub=" + r.PublisherName + " [" + r.PublisherId + "]"
                        + " | SND " + r.ReviewSound
                        + rel;
                }
                else if (mode == "LOW")
                {
                    line = (i + 1) + ". " + r.Name
                        + " | owner=" + r.OwnerName + " [" + r.OwnerId + "]"
                        + " | dev=" + r.DeveloperName + " [" + r.DeveloperId + "]"
                        + " | pub=" + r.PublisherName + " [" + r.PublisherId + "]"
                        + " | TOTAL " + r.ReviewTotal
                        + rel;
                }
                else if (mode == "SELF")
                {
                    line = (i + 1) + ". " + r.Name
                        + " | dev=" + r.DeveloperId
                        + " | TOTAL " + r.ReviewTotal
                        + " | rel=" + r.ReleaseYear + "/" + r.ReleaseMonth + "/" + r.ReleaseWeek
                        + " | dbg=" + r.ReleaseDebug;
                }
                else
                {
                    line = (i + 1) + ". " + r.Name
                        + " | dev=" + r.DeveloperId
                        + " | TOTAL " + r.ReviewTotal
                        + rel;
                }

                if (r.IsSelfRelated)
                    GUILayout.Label("<color=cyan>" + line + "</color>", _labelStyle);
                else
                    GUILayout.Label(line, _labelStyle);
            }

            GUILayout.EndVertical();
            GUILayout.Space(6);
        }

        private void DrawCompanySection(string title, List<CompanyScoreRow> rows, string mode)
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("=== " + title + " ===", _headerStyle);

            if (rows.Count == 0)
            {
                GUILayout.Label("(aucun)", _labelStyle);
                GUILayout.EndVertical();
                GUILayout.Space(6);
                return;
            }

            int limit = _compact ? 5 : 10;
            for (int i = 0; i < rows.Count && i < limit; i++)
            {
                CompanyScoreRow r = rows[i];
                string label;

                if (mode == "STUDIO")
                {
                    label = (i + 1) + ". company=" + r.CompanyId +
                            " | name=" + r.CompanyName +
                            " | StudioPts " + r.Score.ToString("0.0");
                }
                else
                {
                    label = (i + 1) + ". company=" + r.CompanyId +
                            " | name=" + r.CompanyName +
                            " | PubPts " + r.Score.ToString("0.0");
                }

                if (r.IsSelf)
                    GUILayout.Label("<color=cyan>" + label + "</color>", _labelStyle);
                else
                    GUILayout.Label(label, _labelStyle);
            }

            GUILayout.EndVertical();
            GUILayout.Space(6);
        }

        private void DrawDevRow(DevTaskRow r)
        {
            GUILayout.BeginVertical(_boxStyle);

            GUILayout.Label("[" + r.TaskKind + "] " + r.GameName, _headerStyle);
            GUILayout.Label("room=" + r.RoomName +
                            "  |  roomID=" + r.RoomId +
                            "  |  typ=" + r.RoomType +
                            "  |  taskID=" + r.TaskId, _labelStyle);

            GUILayout.Label("IDs owner/dev/pub: " + r.OwnerId + " / " + r.DeveloperId + " / " + r.PublisherId, _labelStyle);
            GUILayout.Label("Flags inDev/shelf/released: " + r.InDevelopment + " / " + r.IsShelf + " / " + r.IsReleased, _labelStyle);
            GUILayout.Label("progress=" + r.Progress.ToString("0.0") + "%", _labelStyle);

            GUILayout.Label("Prévision globale : <b>" + r.Total.ToString("0.0") + "</b>", _labelStyle);

            float need = 80f - r.Total;
            if (need > 0)
                GUILayout.Label("<color=red>Il manque +" + need.ToString("0.0") + " pour atteindre 80</color>", _labelStyle);
            else
                GUILayout.Label("<color=cyan>80 atteint</color>", _labelStyle);

            if (!_compact)
            {
                GUILayout.Label("skillPenalty genre/theme/total="
                    + r.EstimatedGenrePenalty.ToString("0.0") + "/"
                    + r.EstimatedThemePenalty.ToString("0.0") + "/"
                    + r.EstimatedSkillPenalty.ToString("0.0"), _labelStyle);

                GUILayout.Label("levels genre(main/sub)="
                    + r.MainGenreLevel + "/" + r.SubGenreLevel
                    + " | theme(main/sub)="
                    + r.MainThemeLevel + "/" + r.SubThemeLevel, _labelStyle);

                if (r.UnexplainedPenalty > 0.1f)
                {
                    GUILayout.Label("<color=yellow>Perte inexpliquée (compétence/compatibilité suspectée) : -"
                        + r.UnexplainedPenalty.ToString("0.0") + "</color>", _labelStyle);
                }

                GUILayout.Label("actual gp/gfx/snd/ctrl/total="
                    + r.ActReviewGp.ToString("0") + "/"
                    + r.ActReviewGfx.ToString("0") + "/"
                    + r.ActReviewSnd.ToString("0") + "/"
                    + r.ActReviewCtrl.ToString("0") + "/"
                    + r.ActReviewTotal.ToString("0"), _labelStyle);

                GUILayout.Label("pred gp/gfx/snd/ctrl/total="
                    + r.Gp.ToString("0.0") + "/"
                    + r.Gfx.ToString("0.0") + "/"
                    + r.Snd.ToString("0.0") + "/"
                    + r.Ctrl.ToString("0.0") + "/"
                    + r.Total.ToString("0.0"), _labelStyle);

                GUILayout.Label("baseNum=" + r.BaseNum.ToString("0.0"), _labelStyle);
                GUILayout.Label("weights gp/gfx/snd/ctrl="
                    + r.WeightGp.ToString("0") + "/"
                    + r.WeightGfx.ToString("0") + "/"
                    + r.WeightSnd.ToString("0") + "/"
                    + r.WeightCtrl.ToString("0"), _labelStyle);

                if (r.Elements.Count > 0)
                {
                    GUILayout.Label("=== Facteurs ===", _headerStyle);
                    for (int i = 0; i < r.Elements.Count; i++)
                    {
                        string color = "white";
                        if (r.Elements[i].Value < 0) color = "red";
                        else if (r.Elements[i].Value > 0) color = "cyan";

                        GUILayout.Label("<color=" + color + ">"
                            + r.Elements[i].Text + " (" + r.Elements[i].Value.ToString("0.0") + ")</color>", _labelStyle);
                    }
                }

                if (r.Tips.Count > 0)
                {
                    GUILayout.Label("=== Priorité d'amélioration ===", _headerStyle);
                    for (int i = 0; i < r.Tips.Count; i++)
                        GUILayout.Label(r.Tips[i], _labelStyle);
                }
            }

            GUILayout.EndVertical();
            GUILayout.Space(6);
        }

        private void EnsureStyle()
        {
            if (_labelStyle != null && _labelStyle.fontSize == _fontSize.Value)
                return;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = _fontSize.Value;
            _labelStyle.richText = true;
            _labelStyle.wordWrap = true;
            _labelStyle.alignment = TextAnchor.UpperLeft;

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = _fontSize.Value + 2;
            _headerStyle.richText = true;
            _headerStyle.wordWrap = true;
            _headerStyle.alignment = TextAnchor.UpperLeft;
            _headerStyle.fontStyle = FontStyle.Bold;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.alignment = TextAnchor.UpperLeft;
            _boxStyle.wordWrap = true;
            _boxStyle.padding = new RectOffset(8, 8, 6, 6);
        }

        private void TryResolveMainScript()
        {
            if (_mainScript != null)
                return;

            object bestMain = null;
            GameObject bestGo = null;
            int bestScore = int.MinValue;

            MonoBehaviour[] all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null)
                    continue;
                if (mb.GetType().Name != "mainScript")
                    continue;

                GameObject go = mb.gameObject;
                if (go == null)
                    continue;

                int score = 0;

                try
                {
                    if (GetFieldOrPropertyValue(mb, "arrayRoomScripts") != null) score += 5;
                    if (GetFieldOrPropertyValue(mb, "games_") != null) score += 3;

                    int year = GetInt(mb, "year");
                    int month = GetInt(mb, "month");
                    int week = GetInt(mb, "week");
                    int myId = GetInt(mb, "myID");

                    if (year > 1976) score += 4;
                    if (month != 1 || week != 1) score += 2;
                    if (myId > 0) score += 2;

                    if (go.activeInHierarchy) score += 2;
                    if (go.scene.IsValid()) score += 2;

                    if (year == 1976 && month == 1 && week == 1 && myId <= 0)
                        score -= 100;
                }
                catch
                {
                    score -= 100;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMain = mb;
                    bestGo = go;
                }
            }

            _mainScript = bestMain;
            _resolvedMainGameObject = bestGo;
        }

        // ---------------------------
        // DEV
        // ---------------------------
        private void RefreshDevRows()
        {
            try
            {
                _devRows.Clear();

                if (_mainScript == null)
                {
                    _devStatus = "mainScript non résolu";
                    _devDebugInfo = "";
                    return;
                }

                object[] rooms = GetArray(_mainScript, "arrayRoomScripts");
                if (rooms == null)
                {
                    _devStatus = "arrayRoomScripts introuvable";
                    _devDebugInfo = "";
                    return;
                }

                int dbgYear = GetInt(_mainScript, "year");
                int dbgMonth = GetInt(_mainScript, "month");
                int dbgWeek = GetInt(_mainScript, "week");
                int dbgMyId = GetInt(_mainScript, "myID");

                int roomCount = rooms.Length;
                int taskIdCount = 0;
                int taskFound = 0;
                int gsFound = 0;

                for (int i = 0; i < rooms.Length; i++)
                {
                    object room = rooms[i];
                    if (room == null)
                        continue;

                    int taskId = GetInt(room, "taskID");
                    if (taskId != -1)
                        taskIdCount++;

                    AddDevRowFromTask(room, "GetTaskGame", "DEV", ref taskFound, ref gsFound);
                    AddDevRowFromTask(room, "GetTaskBugfixing", "QA", ref taskFound, ref gsFound);
                    AddDevRowFromTask(room, "GetTaskGrafikVerbessern", "GFX", ref taskFound, ref gsFound);
                    AddDevRowFromTask(room, "GetTaskSoundVerbessern", "SND", ref taskFound, ref gsFound);
                    AddDevRowFromTask(room, "GetTaskAnimationVerbessern", "ANIM", ref taskFound, ref gsFound);
                }

                _devStatus = "OK";
                _devDebugInfo =
                    "year=" + dbgYear + " month=" + dbgMonth + " week=" + dbgWeek + " myID=" + dbgMyId + "\n" +
                    "rooms=" + roomCount +
                    " / taskID!=-1=" + taskIdCount +
                    " / taskFound=" + taskFound +
                    " / gS_ found=" + gsFound +
                    " / rows=" + _devRows.Count;
            }
            catch (Exception ex)
            {
                _devStatus = "Erreur de mise à jour : " + ex.GetType().Name + " / " + ex.Message;
                _devDebugInfo = "";
                Logger.LogError(ex);
                ResetResolvedReferences();
            }
        }

        private void AddDevRowFromTask(object room, string methodName, string kind, ref int taskFound, ref int gsFound)
        {
            object task = InvokeObject(room, methodName);
            if (task == null)
                return;

            taskFound++;

            object g = GetFieldOrPropertyValue(task, "gS_");
            if (g == null)
            {
                TryInvokeVoid(task, "FindMyGame");
                g = GetFieldOrPropertyValue(task, "gS_");
            }

            if (g == null)
                return;

            gsFound++;

            DevTaskRow row = BuildDevRow(room, task, g, kind);
            if (row != null)
                _devRows.Add(row);
        }

        private DevTaskRow BuildDevRow(object room, object task, object g, string kind)
        {
            DevTaskRow r = new DevTaskRow();

            r.TaskKind = kind;
            r.RoomId = GetInt(room, "myID");
            r.RoomType = GetInt(room, "typ");
            r.RoomName = SafeRoomName(room);

            r.TaskId = GetInt(task, "myID");

            r.GameName =
                TryInvokeString(g, "GetNameWithTag") ??
                TryInvokeString(g, "GetNameSimple") ??
                TryInvokeString(g, "GetName") ??
                "unknown";

            r.OwnerId = GetInt(g, "ownerID");
            r.DeveloperId = GetInt(g, "developerID");
            r.PublisherId = GetInt(g, "publisherID");

            r.InDevelopment = GetBool(g, "inDevelopment");
            r.IsShelf = GetBool(g, "schublade");
            r.IsReleased = GetInt(g, "weeksOnMarket") > 0;

            r.Progress = ToFloat(InvokeObject(task, "GetProzent"));

            r.ActReviewGp = GetInt(g, "reviewGameplay");
            r.ActReviewGfx = GetInt(g, "reviewGrafik");
            r.ActReviewSnd = GetInt(g, "reviewSound");
            r.ActReviewCtrl = GetInt(g, "reviewSteuerung");
            r.ActReviewTotal = GetInt(g, "reviewTotal");

            r.MainGenre = GetInt(g, "maingenre");
            r.SubGenre = GetInt(g, "subgenre");
            r.MainTheme = GetInt(g, "gameMainTheme");
            r.SubTheme = GetInt(g, "gameSubTheme");

            PredictScores(r, g);
            Analyze(r, g);

            return r;
        }

        private void AnalyzeSkillPenalty(DevTaskRow r, object g)
        {
            r.EstimatedSkillPenalty = 0f;
            r.EstimatedGenrePenalty = 0f;
            r.EstimatedThemePenalty = 0f;

            object genres = GetFieldOrPropertyValue(g, "genres_");
            object themes = GetFieldOrPropertyValue(g, "themes_");

            if (genres != null)
            {
                r.MainGenreLevel = GetIntArrayValue(genres, "genres_LEVEL", r.MainGenre, 5);
                r.SubGenreLevel = r.SubGenre >= 0 ? GetIntArrayValue(genres, "genres_LEVEL", r.SubGenre, 5) : 5;

                float mainGenrePenalty = (5f - r.MainGenreLevel) * 0.6f;
                float subGenrePenalty = r.SubGenre >= 0 ? (5f - r.SubGenreLevel) * 0.3f : 1.5f;

                if (mainGenrePenalty < 0f) mainGenrePenalty = 0f;
                if (subGenrePenalty < 0f) subGenrePenalty = 0f;

                r.EstimatedGenrePenalty = mainGenrePenalty + subGenrePenalty;
            }
            else
            {
                r.MainGenreLevel = 5;
                r.SubGenreLevel = 5;
            }

            if (themes != null)
            {
                r.MainThemeLevel = GetIntArrayValue(themes, "themes_LEVEL", r.MainTheme, 5);
                r.SubThemeLevel = r.SubTheme >= 0 ? GetIntArrayValue(themes, "themes_LEVEL", r.SubTheme, 5) : 5;

                float mainThemePenalty = (5f - r.MainThemeLevel) * 0.6f;
                float subThemePenalty = r.SubTheme >= 0 ? (5f - r.SubThemeLevel) * 0.3f : 1.5f;

                if (mainThemePenalty < 0f) mainThemePenalty = 0f;
                if (subThemePenalty < 0f) subThemePenalty = 0f;

                r.EstimatedThemePenalty = mainThemePenalty + subThemePenalty;
            }
            else
            {
                r.MainThemeLevel = 5;
                r.SubThemeLevel = 5;
            }

            r.EstimatedSkillPenalty = r.EstimatedGenrePenalty + r.EstimatedThemePenalty;
        }

        private void PredictScores(DevTaskRow r, object g)
        {
            object mS = GetFieldOrPropertyValue(g, "mS_");
            object games = GetFieldOrPropertyValue(g, "games_");
            object unlock = GetFieldOrPropertyValue(g, "unlock_");
            object genres = GetFieldOrPropertyValue(g, "genres_");

            if (mS == null || games == null)
                return;

            int difficulty = GetInt(mS, "difficulty");
            int year = GetInt(mS, "year");
            int myId = GetInt(mS, "myID");

            bool retro = GetBool(g, "retro");
            int developerId = GetInt(g, "developerID");
            bool isMyGame = developerId == myId;

            float reviewCurve = InvokeFloatWithArgs(games, "GetReviewCurve");
            float baseNum = 0f;

            if (isMyGame)
            {
                if (!retro)
                {
                    switch (difficulty)
                    {
                        case 0: baseNum = 7000f * reviewCurve; break;
                        case 1: baseNum = 10000f * reviewCurve; break;
                        case 2: baseNum = 15000f * reviewCurve; break;
                        case 3: baseNum = 18000f * reviewCurve; break;
                        case 4: baseNum = 22000f * reviewCurve; break;
                        case 5: baseNum = 30000f * reviewCurve; break;
                    }
                }
                else
                {
                    switch (difficulty)
                    {
                        case 0: baseNum = 2500f * reviewCurve; break;
                        case 1: baseNum = 3000f * reviewCurve; break;
                        case 2: baseNum = 3500f * reviewCurve; break;
                        case 3: baseNum = 4000f * reviewCurve; break;
                        case 4: baseNum = 4200f * reviewCurve; break;
                        case 5: baseNum = 4500f * reviewCurve; break;
                    }
                }
            }
            else if (!retro)
            {
                baseNum = 14000f * reviewCurve;
            }
            else
            {
                baseNum = 4000f * reviewCurve;
            }

            r.BaseNum = baseNum;

            float gpPoints = ToFloat(GetFieldOrPropertyValue(g, "points_gameplay"));
            float gfxPoints = ToFloat(GetFieldOrPropertyValue(g, "points_grafik"));
            float sndPoints = ToFloat(GetFieldOrPropertyValue(g, "points_sound"));
            float ctrlPoints = ToFloat(GetFieldOrPropertyValue(g, "points_technik"));

            float gp = year >= 1979 ? gpPoints / (baseNum / 100f) : gpPoints / (baseNum / 90f);
            float gfx = year >= 1982 ? gfxPoints / (baseNum / 100f) : gfxPoints / (baseNum / 90f);
            float snd = year >= 1985 ? sndPoints / (baseNum / 100f) : sndPoints / (baseNum / 90f);

            bool motionCaptureUnlocked = unlock != null && InvokeBoolWithArgs(unlock, "Get", 8);
            float ctrl = motionCaptureUnlocked ? ctrlPoints / (baseNum / 100f) : ctrlPoints / (baseNum / 80f);

            if (gp > 99f) gp = 99f;
            if (gfx > 99f) gfx = 99f;
            if (snd > 99f) snd = 99f;
            if (ctrl > 99f) ctrl = 99f;

            r.Gp = gp;
            r.Gfx = gfx;
            r.Snd = snd;
            r.Ctrl = ctrl;

            int mainGenre = GetInt(g, "maingenre");
            int wGp = 25;
            int wGfx = 25;
            int wSnd = 25;
            int wCtrl = 25;

            if (genres != null && mainGenre >= 0)
            {
                wGp = GetIntArrayValue(genres, "genres_GAMEPLAY", mainGenre, 25);
                wGfx = GetIntArrayValue(genres, "genres_GRAPHIC", mainGenre, 25);
                wSnd = GetIntArrayValue(genres, "genres_SOUND", mainGenre, 25);
                wCtrl = GetIntArrayValue(genres, "genres_CONTROL", mainGenre, 25);
            }

            r.WeightGp = wGp;
            r.WeightGfx = wGfx;
            r.WeightSnd = wSnd;
            r.WeightCtrl = wCtrl;

            float total = 0f;
            total += gp * 0.01f * wGp;
            total += gfx * 0.01f * wGfx;
            total += snd * 0.01f * wSnd;
            total += ctrl * 0.01f * wCtrl;

            if (total < 1f) total = 1f;
            if (total > 100f) total = 100f;

            r.Total = total;
        }

        private void Analyze(DevTaskRow r, object g)
        {
            r.Elements.Clear();
            r.Tips.Clear();

            AnalyzeSkillPenalty(r, g);

            object genres = GetFieldOrPropertyValue(g, "genres_");
            object themes = GetFieldOrPropertyValue(g, "themes_");

            bool targetOk = true;
            bool genreComboOk = true;
            bool mainThemeOk = true;
            bool subThemeOk = true;

            int targetGroup = GetInt(g, "zielgruppe");
            bool hasTargetGroup = GetFieldOrPropertyValue(g, "zielgruppe") != null;

            if (!hasTargetGroup)
            {
                targetGroup = GetInt(g, "targetGroup");
                hasTargetGroup = GetFieldOrPropertyValue(g, "targetGroup") != null;
            }

            if (!hasTargetGroup)
            {
                targetGroup = GetInt(g, "gameTargetGroup");
                hasTargetGroup = GetFieldOrPropertyValue(g, "gameTargetGroup") != null;
            }

            if (genres != null)
            {
                if (hasTargetGroup)
                {
                    object targetResult = InvokeAny(genres, "IsTargetGroup", r.MainGenre, targetGroup);
                    if (!(targetResult is bool))
                        targetResult = InvokeAny(genres, "IsTargetGroup", targetGroup, r.MainGenre);

                    if (targetResult is bool)
                        targetOk = (bool)targetResult;
                }

                if (r.SubGenre >= 0)
                {
                    object comboResult = InvokeAny(genres, "IsGenreCombination", r.MainGenre, r.SubGenre);
                    if (comboResult is bool)
                        genreComboOk = (bool)comboResult;
                }
            }

            if (themes != null)
            {
                if (r.MainTheme >= 0)
                {
                    object mainThemeResult = InvokeAny(themes, "IsThemesFitWithGenre", r.MainGenre, r.MainTheme);
                    if (!(mainThemeResult is bool))
                        mainThemeResult = InvokeAny(themes, "IsThemesFitWithGenre", r.MainTheme, r.MainGenre);

                    if (mainThemeResult is bool)
                        mainThemeOk = (bool)mainThemeResult;
                }

                if (r.SubTheme >= 0)
                {
                    object subThemeResult = InvokeAny(themes, "IsThemesFitWithGenre", r.MainGenre, r.SubTheme);
                    if (!(subThemeResult is bool))
                        subThemeResult = InvokeAny(themes, "IsThemesFitWithGenre", r.SubTheme, r.MainGenre);

                    if (subThemeResult is bool)
                        subThemeOk = (bool)subThemeResult;
                }
            }

            if (!targetOk)
            {
                r.Elements.Add(new DeltaElement("Incompatibilité du public cible", -3f));
                r.Tips.Add("Vérifiez la compatibilité avec le public cible");
            }

            if (!genreComboOk)
            {
                r.Elements.Add(new DeltaElement("Combinaison de genres incompatible", -3f));
                r.Tips.Add("Vérifiez la compatibilité du genre principal / secondaire");
            }

            if (!mainThemeOk)
            {
                r.Elements.Add(new DeltaElement("Thème principal incompatible", -3f));
                r.Tips.Add("Vérifiez la compatibilité du thème avec le genre");
            }

            if (!subThemeOk)
            {
                r.Elements.Add(new DeltaElement("Thème secondaire incompatible", -1.5f));
                r.Tips.Add("Vérifiez la compatibilité du thème secondaire");
            }

            if (r.EstimatedGenrePenalty > 0.1f)
            {
                r.Elements.Add(new DeltaElement("Pénalité de compétence de genre (estimée)", -r.EstimatedGenrePenalty));
                r.Tips.Add("Compétence de genre : principal Nv" + r.MainGenreLevel + " / secondaire Nv" + r.SubGenreLevel);
            }

            if (r.EstimatedThemePenalty > 0.1f)
            {
                r.Elements.Add(new DeltaElement("Pénalité de compétence de thème (estimée)", -r.EstimatedThemePenalty));
                r.Tips.Add("Compétence de thème : principal Nv" + r.MainThemeLevel + " / secondaire Nv" + r.SubThemeLevel);
            }

            float gpDef = 80f - r.Gp;
            if (gpDef > 0)
            {
                r.Elements.Add(new DeltaElement("Gameplay sous l'objectif", -gpDef));
                r.Tips.Add("Améliorer le gameplay +" + gpDef.ToString("0.0"));
            }

            float gfxDef = 80f - r.Gfx;
            if (gfxDef > 0)
            {
                r.Elements.Add(new DeltaElement("Graphismes sous l'objectif", -gfxDef));
                r.Tips.Add("Améliorer les graphismes +" + gfxDef.ToString("0.0"));
            }

            float ctrlDef = 80f - r.Ctrl;
            if (ctrlDef > 0)
            {
                r.Elements.Add(new DeltaElement("Contrôle sous l'objectif", -ctrlDef));
                r.Tips.Add("Améliorer le contrôle +" + ctrlDef.ToString("0.0"));
            }

            float sndDef = 80f - r.Snd;
            if (sndDef > 0)
            {
                r.Elements.Add(new DeltaElement("Son sous l'objectif", -sndDef));
                r.Tips.Add("Améliorer le son +" + sndDef.ToString("0.0"));
            }

            int gameSize = GetInt(g, "gameSize");
            if (gameSize == 0)
            {
                r.Elements.Add(new DeltaElement("Pénalité de taille du jeu", -3f));
            }

            if (GetBool(g, "engineIsOld"))
            {
                r.Elements.Add(new DeltaElement("Pénalité de moteur ancien", -5f));
            }

            float bugs = ToFloat(GetFieldOrPropertyValue(g, "points_bugs"));
            if (bugs > 0)
            {
                r.Elements.Add(new DeltaElement("Bugs", -bugs));
            }

            float marketing = ToFloat(GetFieldOrPropertyValue(g, "marketingBonus"));
            if (Math.Abs(marketing) > 0.001f)
            {
                r.Elements.Add(new DeltaElement("Bonus marketing", marketing));
            }

            if (GetBool(g, "typ_contractGame"))
            {
                r.Elements.Add(new DeltaElement("Pénalité de jeu sous contrat", -2f));
            }

            if (GetInt(g, "portID") != -1)
            {
                r.Elements.Add(new DeltaElement("Pénalité de portage", -3f));
            }

            r.UnexplainedPenalty = 0f;

            if (r.ActReviewTotal > 0)
            {
                float knownPenalty = 0f;

                for (int i = 0; i < r.Elements.Count; i++)
                {
                    if (r.Elements[i].Value < 0f)
                        knownPenalty += -r.Elements[i].Value;
                }

                float rawGap = r.Total - r.ActReviewTotal;
                if (rawGap < 0f)
                    rawGap = 0f;

                r.UnexplainedPenalty = rawGap - knownPenalty;
                if (r.UnexplainedPenalty < 0f)
                    r.UnexplainedPenalty = 0f;

                if (r.UnexplainedPenalty > 1.0f)
                {
                    r.Elements.Add(new DeltaElement("Perte inexpliquée (compétence/compatibilité suspectée)", -r.UnexplainedPenalty));
                    r.Tips.Add("Des pénalités cachées existent");
                }
            }
        }

        // ---------------------------
        // AWARDS
        // ---------------------------
        private void RefreshAwardRows()
        {
            try
            {
                _gfxAwardRows.Clear();
                _soundAwardRows.Clear();
                _gotyRows.Clear();
                _worstRows.Clear();
                _selfRows.Clear();
                _studioAwardRows.Clear();
                _publisherAwardRows.Clear();

                if (_mainScript == null)
                {
                    _awardStatus = "mainScript non résolu";
                    _awardDebugInfo = "";
                    return;
                }

                object games = GetFieldOrPropertyValue(_mainScript, "games_");
                if (games == null)
                {
                    _awardStatus = "games_ introuvable";
                    _awardDebugInfo = "";
                    return;
                }

                object[] allGames = GetArray(games, "arrayGamesScripts");
                if (allGames == null)
                {
                    _awardStatus = "arrayGamesScripts introuvable";
                    _awardDebugInfo = "";
                    return;
                }

                int myId = GetInt(_mainScript, "myID");
                int currentYear = GetInt(_mainScript, "year");
                int currentMonth = GetInt(_mainScript, "month");
                int currentWeek = GetInt(_mainScript, "week");

                int awardStartYear;

                if (currentMonth >= 12)
                    awardStartYear = currentYear;
                else
                    awardStartYear = currentYear - 1;

                int awardStartMonth = 12;
                int awardStartWeek = 1;

                int startStamp = MakeDateStamp(awardStartYear, awardStartMonth, awardStartWeek);
                int nowStamp = MakeDateStamp(currentYear, currentMonth, currentWeek);

                int releasedCount = 0;
                int inPeriodCount = 0;
                int selfCount = 0;
                int unknownReleaseCount = 0;

                List<AwardGameRow> allReleasedInPeriod = new List<AwardGameRow>();

                Dictionary<int, float> studioScoreMap = new Dictionary<int, float>();
                Dictionary<int, float> publisherScoreMap = new Dictionary<int, float>();

                Dictionary<int, string> studioNameMap = new Dictionary<int, string>();
                Dictionary<int, string> publisherNameMap = new Dictionary<int, string>();

                for (int i = 0; i < allGames.Length; i++)
                {
                    object g = allGames[i];
                    if (g == null)
                        continue;

                    int weeksOnMarket = GetInt(g, "weeksOnMarket");
                    bool shelf = GetBool(g, "schublade");

                    if (weeksOnMarket <= 0)
                        continue;

                    if (shelf)
                        continue;

                    releasedCount++;

                    AwardGameRow row = BuildAwardRow(g, myId);

                    if (row.ReleaseStamp <= 0)
                    {
                        unknownReleaseCount++;
                        continue;
                    }

                    if (row.ReleaseStamp < startStamp)
                        continue;

                    if (row.ReleaseStamp > nowStamp)
                        continue;

                    inPeriodCount++;
                    allReleasedInPeriod.Add(row);

                    float studioPts = GetStudioPoints(g);
                    float publisherPts = GetPublisherPoints(g);

                    int devId = row.DeveloperId;
                    int pubId = row.PublisherId;

                    if (devId > 0)
                    {
                        if (!studioNameMap.ContainsKey(devId) && !string.IsNullOrEmpty(row.DeveloperName))
                            studioNameMap[devId] = row.DeveloperName;
                    }

                    if (pubId > 0)
                    {
                        if (!publisherNameMap.ContainsKey(pubId) && !string.IsNullOrEmpty(row.PublisherName))
                            publisherNameMap[pubId] = row.PublisherName;
                    }

                    if (devId > 0)
                    {
                        float cur;
                        if (!studioScoreMap.TryGetValue(devId, out cur))
                            cur = 0f;
                        studioScoreMap[devId] = cur + studioPts;
                    }

                    if (pubId > 0)
                    {
                        float cur;
                        if (!publisherScoreMap.TryGetValue(pubId, out cur))
                            cur = 0f;
                        publisherScoreMap[pubId] = cur + publisherPts;
                    }

                    if (row.IsSelfRelated)
                    {
                        selfCount++;
                        _selfRows.Add(row);
                    }
                }

                List<AwardGameRow> tmp = new List<AwardGameRow>(allReleasedInPeriod);

                tmp.Sort(delegate (AwardGameRow a, AwardGameRow b)
                {
                    int c = b.ReviewGrafik.CompareTo(a.ReviewGrafik);
                    if (c != 0) return c;
                    return b.ReviewTotal.CompareTo(a.ReviewTotal);
                });
                CopyTop(tmp, _gfxAwardRows, 10);

                tmp = new List<AwardGameRow>(allReleasedInPeriod);
                tmp.Sort(delegate (AwardGameRow a, AwardGameRow b)
                {
                    int c = b.ReviewSound.CompareTo(a.ReviewSound);
                    if (c != 0) return c;
                    return b.ReviewTotal.CompareTo(a.ReviewTotal);
                });
                CopyTop(tmp, _soundAwardRows, 10);

                tmp = new List<AwardGameRow>(allReleasedInPeriod);
                tmp.Sort(delegate (AwardGameRow a, AwardGameRow b)
                {
                    int c = b.ReviewTotal.CompareTo(a.ReviewTotal);
                    if (c != 0) return c;
                    c = b.ReviewGameplay.CompareTo(a.ReviewGameplay);
                    if (c != 0) return c;
                    return b.ReviewGrafik.CompareTo(a.ReviewGrafik);
                });
                CopyTop(tmp, _gotyRows, 10);

                tmp = new List<AwardGameRow>(allReleasedInPeriod);
                tmp.Sort(delegate (AwardGameRow a, AwardGameRow b)
                {
                    int c = a.ReviewTotal.CompareTo(b.ReviewTotal);
                    if (c != 0) return c;
                    return a.ReviewGameplay.CompareTo(b.ReviewGameplay);
                });
                CopyTop(tmp, _worstRows, 10);

                foreach (KeyValuePair<int, float> kv in studioScoreMap)
                {
                    CompanyScoreRow r = new CompanyScoreRow();
                    r.CompanyId = kv.Key;

                    string name;
                    if (!studioNameMap.TryGetValue(kv.Key, out name))
                        name = GetCompanyNameById(kv.Key);

                    r.CompanyName = name;
                    r.Score = kv.Value;
                    r.IsSelf = (kv.Key == myId);
                    _studioAwardRows.Add(r);
                }

                foreach (KeyValuePair<int, float> kv in publisherScoreMap)
                {
                    CompanyScoreRow r = new CompanyScoreRow();
                    r.CompanyId = kv.Key;

                    string name;
                    if (!publisherNameMap.TryGetValue(kv.Key, out name))
                        name = GetCompanyNameById(kv.Key);

                    r.CompanyName = name;
                    r.Score = kv.Value;
                    r.IsSelf = (kv.Key == myId);
                    _publisherAwardRows.Add(r);
                }

                _studioAwardRows.Sort(delegate (CompanyScoreRow a, CompanyScoreRow b)
                {
                    return b.Score.CompareTo(a.Score);
                });

                _publisherAwardRows.Sort(delegate (CompanyScoreRow a, CompanyScoreRow b)
                {
                    return b.Score.CompareTo(a.Score);
                });

                _selfRows.Sort(delegate (AwardGameRow a, AwardGameRow b)
                {
                    int c = b.ReviewTotal.CompareTo(a.ReviewTotal);
                    if (c != 0) return c;
                    return b.ReleaseStamp.CompareTo(a.ReleaseStamp);
                });

                _awardStatus = "OK";
                _awardDebugInfo =
                    "period=" + awardStartYear + "/12/1 - " + currentYear + "/" + currentMonth + "/" + currentWeek + "\n" +
                    "releasedAll=" + releasedCount +
                    " / releasedInPeriod=" + inPeriodCount +
                    " / selfReleased=" + selfCount +
                    " / unknownRelease=" + unknownReleaseCount;
            }
            catch (Exception ex)
            {
                _awardStatus = "Erreur de mise à jour : " + ex.GetType().Name + " / " + ex.Message;
                _awardDebugInfo = "";
                Logger.LogError(ex);
            }
        }

        private AwardGameRow BuildAwardRow(object g, int myId)
        {
            AwardGameRow r = new AwardGameRow();

            r.Name =
                TryInvokeString(g, "GetNameWithTag") ??
                TryInvokeString(g, "GetNameSimple") ??
                TryInvokeString(g, "GetName") ??
                "unknown";

            r.OwnerId = GetInt(g, "ownerID");
            r.DeveloperId = GetInt(g, "developerID");
            r.PublisherId = GetInt(g, "publisherID");

            string ownerName = TryInvokeString(g, "GetOwnerName");
            if (string.IsNullOrEmpty(ownerName))
                ownerName = "ID:" + r.OwnerId;
            r.OwnerName = ownerName;

            string developerName = TryInvokeString(g, "GetDeveloperName");
            if (string.IsNullOrEmpty(developerName))
                developerName = "ID:" + r.DeveloperId;
            r.DeveloperName = developerName;

            string publisherName = TryInvokeString(g, "GetPublisherName");
            if (string.IsNullOrEmpty(publisherName))
                publisherName = "ID:" + r.PublisherId;
            r.PublisherName = publisherName;

            r.ReviewGameplay = GetInt(g, "reviewGameplay");
            r.ReviewGrafik = GetInt(g, "reviewGrafik");
            r.ReviewSound = GetInt(g, "reviewSound");
            r.ReviewControl = GetInt(g, "reviewSteuerung");
            r.ReviewTotal = GetInt(g, "reviewTotal");

            r.IsSelfRelated =
                r.OwnerId == myId ||
                r.DeveloperId == myId ||
                r.PublisherId == myId;

            r.ReleaseYear = GetReleaseYear(g);
            r.ReleaseMonth = GetReleaseMonth(g);
            r.ReleaseWeek = GetReleaseWeek(g);
            r.ReleaseStamp = MakeDateStamp(r.ReleaseYear, r.ReleaseMonth, r.ReleaseWeek);

            r.ReleaseDebug = DebugReleaseFields(g);

            return r;
        }

        private int GetReleaseYear(object g)
        {
            int v = GetInt(g, "releaseDate_year");
            if (v == 0) v = GetInt(g, "releaseYear");
            if (v == 0) v = GetInt(g, "date_year");
            if (v == 0) v = GetInt(g, "year");
            if (v == 0) v = GetInt(g, "jahr");
            return v;
        }

        private int GetReleaseMonth(object g)
        {
            int v = GetInt(g, "releaseDate_month");
            if (v == 0) v = GetInt(g, "releaseMonth");
            if (v == 0) v = GetInt(g, "date_month");
            if (v == 0) v = GetInt(g, "month");
            if (v == 0) v = GetInt(g, "monat");
            return v;
        }

        private int GetReleaseWeek(object g)
        {
            int v = GetInt(g, "releaseDate_week");
            if (v == 0) v = GetInt(g, "releaseWeek");
            if (v == 0) v = GetInt(g, "date_week");
            if (v == 0) v = GetInt(g, "week");
            if (v == 0) v = GetInt(g, "woche");

            if (v <= 0)
            {
                int y = GetReleaseYear(g);
                int m = GetReleaseMonth(g);
                if (y > 0 && m > 0)
                    v = 1;
            }

            return v;
        }

        private static int MakeDateStamp(int year, int month, int week)
        {
            if (year <= 0 || month <= 0)
                return 0;

            if (week <= 0)
                week = 1;

            return year * 1000 + month * 10 + week;
        }

        private void CopyTop(List<AwardGameRow> src, List<AwardGameRow> dst, int limit)
        {
            dst.Clear();
            for (int i = 0; i < src.Count && i < limit; i++)
                dst.Add(src[i]);
        }

        private float GetStudioPoints(object g)
        {
            float v = ToFloat(GetFieldOrPropertyValue(g, "studioPoints"));
            if (Math.Abs(v) > 0.001f) return v;

            v = ToFloat(GetFieldOrPropertyValue(g, "studioPoints_"));
            if (Math.Abs(v) > 0.001f) return v;

            return GetInt(g, "reviewTotal");
        }

        private float GetPublisherPoints(object g)
        {
            float v = ToFloat(GetFieldOrPropertyValue(g, "salesPoints"));
            if (Math.Abs(v) > 0.001f) return v;

            v = ToFloat(GetFieldOrPropertyValue(g, "publisherPoints"));
            if (Math.Abs(v) > 0.001f) return v;

            v = ToFloat(GetFieldOrPropertyValue(g, "verkaufspunkte"));
            if (Math.Abs(v) > 0.001f) return v;

            return GetInt(g, "reviewTotal");
        }

        private string SafeRoomName(object room)
        {
            string myName = GetString(room, "myName");
            if (!string.IsNullOrEmpty(myName))
                return myName;

            return "Room_" + GetInt(room, "myID");
        }

        private bool NeedsReResolve()
        {
            if (_mainScript == null)
                return false;

            try
            {
                if (_mainScript is UnityEngine.Object && ((UnityEngine.Object)_mainScript) == null)
                    return true;

                int currentYear = GetInt(_mainScript, "year");
                int currentMonth = GetInt(_mainScript, "month");
                int currentWeek = GetInt(_mainScript, "week");

                if (currentYear == 1976 && currentMonth == 1 && currentWeek == 1)
                    return true;
            }
            catch
            {
                return true;
            }

            return false;
        }

        private void ResetResolvedReferences()
        {
            _mainScript = null;
            _resolvedMainGameObject = null;
            _devRows.Clear();
            _gfxAwardRows.Clear();
            _soundAwardRows.Clear();
            _gotyRows.Clear();
            _worstRows.Clear();
            _selfRows.Clear();

            _studioAwardRows.Clear();
            _publisherAwardRows.Clear();
        }

        private static object GetFieldOrPropertyValue(object obj, string name)
        {
            if (obj == null) return null;

            Type t = obj.GetType();

            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(obj);

            PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null) return p.GetValue(obj, null);

            return null;
        }

        private static object[] GetArray(object obj, string name)
        {
            object value = GetFieldOrPropertyValue(obj, name);
            if (value is Array)
            {
                Array arr = (Array)value;
                object[] result = new object[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                    result[i] = arr.GetValue(i);
                return result;
            }
            return null;
        }

        private static object InvokeObject(object obj, string methodName)
        {
            if (obj == null) return null;

            MethodInfo mi = obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return null;

            return mi.Invoke(obj, null);
        }

        private static bool TryInvokeVoid(object obj, string methodName, params object[] args)
        {
            if (obj == null) return false;

            try
            {
                MethodInfo[] methods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo mi = methods[i];
                    if (mi.Name != methodName)
                        continue;

                    ParameterInfo[] pars = mi.GetParameters();
                    if (pars.Length != args.Length)
                        continue;

                    mi.Invoke(obj, args);
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static string TryInvokeString(object obj, string methodName)
        {
            object value = InvokeObject(obj, methodName);
            return value != null ? value.ToString() : null;
        }

        private static float InvokeFloatWithArgs(object obj, string methodName, params object[] args)
        {
            object value = InvokeAny(obj, methodName, args);
            if (value == null) return 0f;
            try { return Convert.ToSingle(value); }
            catch { return 0f; }
        }

        private static bool InvokeBoolWithArgs(object obj, string methodName, params object[] args)
        {
            object value = InvokeAny(obj, methodName, args);
            return value is bool && (bool)value;
        }

        private static object InvokeAny(object obj, string methodName, params object[] args)
        {
            if (obj == null) return null;

            MethodInfo[] methods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo mi = methods[i];
                if (mi.Name != methodName)
                    continue;

                ParameterInfo[] pars = mi.GetParameters();
                if (pars.Length != args.Length)
                    continue;

                try
                {
                    return mi.Invoke(obj, args);
                }
                catch { }
            }

            return null;
        }

        private static float ToFloat(object v)
        {
            if (v == null) return 0f;
            try { return Convert.ToSingle(v); }
            catch { return 0f; }
        }

        private static bool GetBool(object o, string n)
        {
            object v = GetFieldOrPropertyValue(o, n);
            return v is bool && (bool)v;
        }

        private static int GetInt(object o, string n)
        {
            object v = GetFieldOrPropertyValue(o, n);
            if (v == null) return 0;
            try { return Convert.ToInt32(v); }
            catch { return 0; }
        }

        private static string GetString(object o, string n)
        {
            object v = GetFieldOrPropertyValue(o, n);
            return v != null ? v.ToString() : "";
        }

        private static int[] GetIntArray(object obj, string name)
        {
            object value = GetFieldOrPropertyValue(obj, name);
            return value as int[];
        }

        private static int GetIntArrayValue(object obj, string name, int index, int def)
        {
            int[] arr = GetIntArray(obj, name);
            if (arr == null) return def;
            if (index < 0 || index >= arr.Length) return def;
            return arr[index];
        }
    }

    internal sealed class DevTaskRow
    {
        public int MainGenre;
        public int SubGenre;
        public int MainTheme;
        public int SubTheme;

        public int MainGenreLevel;
        public int SubGenreLevel;
        public int MainThemeLevel;
        public int SubThemeLevel;

        public float EstimatedSkillPenalty;
        public float EstimatedGenrePenalty;
        public float EstimatedThemePenalty;
        public float UnexplainedPenalty;

        public string TaskKind;
        public int RoomId;
        public int RoomType;
        public string RoomName;

        public int TaskId;
        public string GameName;

        public int OwnerId;
        public int DeveloperId;
        public int PublisherId;

        public bool InDevelopment;
        public bool IsShelf;
        public bool IsReleased;

        public float Progress;

        public float Gp;
        public float Gfx;
        public float Snd;
        public float Ctrl;
        public float Total;

        public float BaseNum;
        public int WeightGp;
        public int WeightGfx;
        public int WeightSnd;
        public int WeightCtrl;

        public int ActReviewGp;
        public int ActReviewGfx;
        public int ActReviewSnd;
        public int ActReviewCtrl;
        public int ActReviewTotal;

        public List<DeltaElement> Elements = new List<DeltaElement>();
        public List<string> Tips = new List<string>();
    }

    internal sealed class AwardGameRow
    {
        public string OwnerName;
        public string DeveloperName;
        public string PublisherName;
        public string ReleaseDebug;

        public string Name;
        public int OwnerId;
        public int DeveloperId;
        public int PublisherId;

        public int ReviewGameplay;
        public int ReviewGrafik;
        public int ReviewSound;
        public int ReviewControl;
        public int ReviewTotal;

        public bool IsSelfRelated;

        public int ReleaseYear;
        public int ReleaseMonth;
        public int ReleaseWeek;
        public int ReleaseStamp;
    }

    internal sealed class DeltaElement
    {
        public string Text;
        public float Value;

        public DeltaElement(string t, float v)
        {
            Text = t;
            Value = v;
        }
    }

    internal sealed class CompanyScoreRow
    {
        public int CompanyId;
        public string CompanyName;
        public float Score;
        public bool IsSelf;
    }
}