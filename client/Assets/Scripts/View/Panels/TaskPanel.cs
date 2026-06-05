using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Shared.Common.Protos;
using Client.Logic;

namespace Client.View
{
    public partial class TaskPanel : UIPanel
    {
        private const float TASK_ITEM_H = 55f;
        private const float TASK_ITEM_GAP = 4f;

        // 列表区域
        private GameObject _listContent;
        private Text _taskCountText;

        // 详情区域
        private Text _detailTitle;
        private Text _detailState;
        private Text _detailGoals;
        private Text _detailTime;

        // 接取输入
        private InputField _acceptInput;

        // 按钮
        private Button _submitBtn;
        private Button _giveUpBtn;

        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, new Color(0, 0, 0, 0.6f));

            float panelW = 780f;
            float panelH = 600f;
            var (panelRoot, content) = UIBuilder.CreateBorderedPanel(transform, "TaskCard",
                panelW, panelH, UIBuilder.PanelBg, UIBuilder.GoldDark, 2f);
            panelRoot.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            // 标题栏
            var titleBar = UIBuilder.CreateTitleBar(content, "TitleBar", "任 务", panelW, 50);
            var tbRect = titleBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0, 1);
            tbRect.anchorMax = new Vector2(1, 1);
            tbRect.pivot = new Vector2(0.5f, 1);
            tbRect.offsetMin = new Vector2(0, -50);
            tbRect.offsetMax = Vector2.zero;

            _taskCountText = UIBuilder.CreateText(titleBar.transform, "CountText", "0 个", 16,
                UIBuilder.TextDim, TextAnchor.MiddleRight);
            var ctRect = _taskCountText.GetComponent<RectTransform>();
            ctRect.anchorMin = new Vector2(1, 0);
            ctRect.anchorMax = new Vector2(1, 1);
            ctRect.pivot = new Vector2(1, 0.5f);
            ctRect.offsetMin = new Vector2(-80, 0);
            ctRect.offsetMax = new Vector2(-10, 0);

            // 主体：左侧列表 + 右侧详情
            float bodyTop = -56f;
            float bodyH = panelH - 56f - 60f;
            float listW = 300f;

            // 左侧：可滚动任务列表
            var listArea = CreateSection(content, "ListArea", listW, bodyH);
            var laRect = listArea.GetComponent<RectTransform>();
            laRect.anchorMin = new Vector2(0, 1);
            laRect.anchorMax = new Vector2(0, 1);
            laRect.pivot = new Vector2(0, 1);
            laRect.anchoredPosition = new Vector2(10, bodyTop);
            laRect.sizeDelta = new Vector2(listW, bodyH);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(listArea.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = new Vector2(0, 0);
            vpRect.anchorMax = new Vector2(1, 1);
            vpRect.offsetMin = new Vector2(4, 4);
            vpRect.offsetMax = new Vector2(-4, -4);
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            _listContent = new GameObject("Content");
            _listContent.transform.SetParent(viewport.transform, false);
            var lcRect = _listContent.AddComponent<RectTransform>();
            lcRect.anchorMin = new Vector2(0, 1);
            lcRect.anchorMax = new Vector2(1, 1);
            lcRect.pivot = new Vector2(0.5f, 1);
            lcRect.anchoredPosition = Vector2.zero;

            var scrollRect = listArea.AddComponent<ScrollRect>();
            scrollRect.content = lcRect;
            scrollRect.viewport = vpRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            // 右侧：详情区域
            float detailW = panelW - listW - 30f;
            var detailArea = CreateSection(content, "DetailArea", detailW, bodyH);
            var daRect = detailArea.GetComponent<RectTransform>();
            daRect.anchorMin = new Vector2(0, 1);
            daRect.anchorMax = new Vector2(0, 1);
            daRect.pivot = new Vector2(0, 1);
            daRect.anchoredPosition = new Vector2(listW + 20, bodyTop);
            daRect.sizeDelta = new Vector2(detailW, bodyH);

            _detailTitle = UIBuilder.CreateText(detailArea.transform, "DTitle", "选择一个任务查看详情",
                18, UIBuilder.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            var dtRect = _detailTitle.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0, 1);
            dtRect.anchorMax = new Vector2(1, 1);
            dtRect.pivot = new Vector2(0, 1);
            dtRect.offsetMin = new Vector2(10, -30);
            dtRect.offsetMax = new Vector2(-10, -8);

            _detailState = UIBuilder.CreateText(detailArea.transform, "DState", "",
                15, UIBuilder.TextDim, TextAnchor.MiddleLeft);
            var dsRect = _detailState.GetComponent<RectTransform>();
            dsRect.anchorMin = new Vector2(0, 1);
            dsRect.anchorMax = new Vector2(1, 1);
            dsRect.pivot = new Vector2(0, 1);
            dsRect.offsetMin = new Vector2(10, -50);
            dsRect.offsetMax = new Vector2(-10, -34);

            var divLine = UIBuilder.CreateDivider(detailArea.transform, "DivLine", detailW - 20f);
            var dlRect = divLine.GetComponent<RectTransform>();
            dlRect.anchorMin = new Vector2(0.5f, 1);
            dlRect.anchorMax = new Vector2(0.5f, 1);
            dlRect.pivot = new Vector2(0.5f, 1);
            dlRect.anchoredPosition = new Vector2(0, -56);
            dlRect.sizeDelta = new Vector2(detailW - 20f, 2f);

            _detailGoals = UIBuilder.CreateText(detailArea.transform, "DGoals", "",
                14, UIBuilder.TextWhite, TextAnchor.UpperLeft);
            var dgRect = _detailGoals.GetComponent<RectTransform>();
            dgRect.anchorMin = new Vector2(0, 1);
            dgRect.anchorMax = new Vector2(1, 1);
            dgRect.pivot = new Vector2(0, 1);
            dgRect.offsetMin = new Vector2(10, -120);
            dgRect.offsetMax = new Vector2(-10, -64);

            _detailTime = UIBuilder.CreateText(detailArea.transform, "DTime", "",
                12, UIBuilder.TextDim, TextAnchor.MiddleRight);
            var dtmRect = _detailTime.GetComponent<RectTransform>();
            dtmRect.anchorMin = new Vector2(0, 0);
            dtmRect.anchorMax = new Vector2(1, 0);
            dtmRect.pivot = new Vector2(1, 0);
            dtmRect.offsetMin = new Vector2(10, 10);
            dtmRect.offsetMax = new Vector2(-10, 28);

            // 底部按钮
            float bottomY = bodyTop - bodyH - 10f;

            // 接取行：输入框 + 按钮
            _acceptInput = UIBuilder.CreateInputField(content, "AcceptInput", "输入任务ID...", 120, 38, 15);
            _acceptInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(-240, bottomY);

            var acceptBtn = UIBuilder.CreateButton(content, "AcceptBtn", "接 取", 90, 38, 16,
                UIBuilder.AccentBlue, UIBuilder.TextWhite);
            acceptBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-140, bottomY);
            acceptBtn.onClick.AddListener(OnAcceptClick);

            _submitBtn = UIBuilder.CreateButton(content, "SubmitBtn", "提 交", 90, 38, 16,
                UIBuilder.ButtonNormal, UIBuilder.Gold);
            _submitBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-30, bottomY);
            _submitBtn.onClick.AddListener(OnSubmitClick);

            _giveUpBtn = UIBuilder.CreateButton(content, "GiveUpBtn", "放 弃", 90, 38, 16,
                UIBuilder.ButtonNormal, new Color(0.8f, 0.3f, 0.3f));
            _giveUpBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(80, bottomY);
            _giveUpBtn.onClick.AddListener(OnGiveUpClick);

            var closeBtn = UIBuilder.CreateButton(content, "CloseBtn", "关 闭", 90, 38, 16,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(200, bottomY);
            closeBtn.onClick.AddListener(OnCloseClick);

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            TaskController.Instance.QueryTaskList();
        }

        public override void OnHide()
        {
        }
    }
}
