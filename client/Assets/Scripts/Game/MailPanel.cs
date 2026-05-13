using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Client.Core;
using Client.Network;
using Shared.Common.Protos;
using Shared.Common.Config;
using Shared.Core.Config;
using Shared.Common;

namespace Client.Game
{
    public class MailPanel : UIPanel
    {
        private const float MAIL_ITEM_H = 60f;
        private const float MAIL_ITEM_GAP = 4f;

        private List<MailInfo> _mails = new List<MailInfo>();
        private long _selectedMailId = -1;

        // List area
        private GameObject _listContent;
        private Text _mailCountText;

        // Detail area
        private Text _detailTitle;
        private Text _detailSender;
        private Text _detailContent;
        private Text _detailTime;
        private Text _detailAttachments;
        private GameObject _detailPanel;

        // Buttons
        private Button _readBtn;
        private Button _claimBtn;
        private Button _deleteBtn;

        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, new Color(0, 0, 0, 0.6f));

            float panelW = 800f;
            float panelH = 620f;
            var (panelRoot, content) = UIBuilder.CreateBorderedPanel(transform, "MailCard",
                panelW, panelH, UIBuilder.PanelBg, UIBuilder.GoldDark, 2f);
            panelRoot.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            // Title bar
            var titleBar = UIBuilder.CreateTitleBar(content, "TitleBar", "邮 件", panelW, 50);
            var tbRect = titleBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0, 1);
            tbRect.anchorMax = new Vector2(1, 1);
            tbRect.pivot = new Vector2(0.5f, 1);
            tbRect.offsetMin = new Vector2(0, -50);
            tbRect.offsetMax = Vector2.zero;

            _mailCountText = UIBuilder.CreateText(titleBar.transform, "CountText", "0 封", 16,
                UIBuilder.TextDim, TextAnchor.MiddleRight);
            var ctRect = _mailCountText.GetComponent<RectTransform>();
            ctRect.anchorMin = new Vector2(1, 0);
            ctRect.anchorMax = new Vector2(1, 1);
            ctRect.pivot = new Vector2(1, 0.5f);
            ctRect.offsetMin = new Vector2(-80, 0);
            ctRect.offsetMax = new Vector2(-10, 0);

            // Main body: left list + right detail
            float bodyTop = -56f;
            float bodyH = panelH - 56f - 60f;
            float listW = 320f;

            // Left: mail list with scroll
            var listArea = CreateSection(content, "ListArea", listW, bodyH);
            var laRect = listArea.GetComponent<RectTransform>();
            laRect.anchorMin = new Vector2(0, 1);
            laRect.anchorMax = new Vector2(0, 1);
            laRect.pivot = new Vector2(0, 1);
            laRect.anchoredPosition = new Vector2(10, bodyTop);
            laRect.sizeDelta = new Vector2(listW, bodyH);

            // Scroll viewport
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

            // Right: detail area
            float detailW = panelW - listW - 30f;
            var detailArea = CreateSection(content, "DetailArea", detailW, bodyH);
            var daRect = detailArea.GetComponent<RectTransform>();
            daRect.anchorMin = new Vector2(0, 1);
            daRect.anchorMax = new Vector2(0, 1);
            daRect.pivot = new Vector2(0, 1);
            daRect.anchoredPosition = new Vector2(listW + 20, bodyTop);
            daRect.sizeDelta = new Vector2(detailW, bodyH);

            _detailPanel = detailArea;

            _detailTitle = UIBuilder.CreateText(detailArea.transform, "DTitle", "选择一封邮件查看详情",
                18, UIBuilder.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            var dtRect = _detailTitle.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0, 1);
            dtRect.anchorMax = new Vector2(1, 1);
            dtRect.pivot = new Vector2(0, 1);
            dtRect.offsetMin = new Vector2(10, -30);
            dtRect.offsetMax = new Vector2(-10, -8);

            _detailSender = UIBuilder.CreateText(detailArea.transform, "DSender", "",
                14, UIBuilder.TextDim, TextAnchor.MiddleLeft);
            var dsRect = _detailSender.GetComponent<RectTransform>();
            dsRect.anchorMin = new Vector2(0, 1);
            dsRect.anchorMax = new Vector2(1, 1);
            dsRect.pivot = new Vector2(0, 1);
            dsRect.offsetMin = new Vector2(10, -48);
            dsRect.offsetMax = new Vector2(-10, -32);

            var divLine = UIBuilder.CreateDivider(detailArea.transform, "DivLine", detailW - 20f);
            var dlRect = divLine.GetComponent<RectTransform>();
            dlRect.anchorMin = new Vector2(0.5f, 1);
            dlRect.anchorMax = new Vector2(0.5f, 1);
            dlRect.pivot = new Vector2(0.5f, 1);
            dlRect.anchoredPosition = new Vector2(0, -54);
            dlRect.sizeDelta = new Vector2(detailW - 20f, 2f);

            _detailContent = UIBuilder.CreateText(detailArea.transform, "DContent", "",
                15, UIBuilder.TextWhite, TextAnchor.UpperLeft);
            var dcRect = _detailContent.GetComponent<RectTransform>();
            dcRect.anchorMin = new Vector2(0, 1);
            dcRect.anchorMax = new Vector2(1, 1);
            dcRect.pivot = new Vector2(0, 1);
            dcRect.offsetMin = new Vector2(10, -140);
            dcRect.offsetMax = new Vector2(-10, -60);

            _detailAttachments = UIBuilder.CreateText(detailArea.transform, "DAttach", "",
                14, UIBuilder.Gold, TextAnchor.UpperLeft);
            var da2Rect = _detailAttachments.GetComponent<RectTransform>();
            da2Rect.anchorMin = new Vector2(0, 0);
            da2Rect.anchorMax = new Vector2(1, 0);
            da2Rect.pivot = new Vector2(0, 0);
            da2Rect.offsetMin = new Vector2(10, 50);
            da2Rect.offsetMax = new Vector2(-10, 120);

            _detailTime = UIBuilder.CreateText(detailArea.transform, "DTime", "",
                12, UIBuilder.TextDim, TextAnchor.MiddleRight);
            var dtmRect = _detailTime.GetComponent<RectTransform>();
            dtmRect.anchorMin = new Vector2(0, 0);
            dtmRect.anchorMax = new Vector2(1, 0);
            dtmRect.pivot = new Vector2(1, 0);
            dtmRect.offsetMin = new Vector2(10, 30);
            dtmRect.offsetMax = new Vector2(-10, 48);

            // Bottom buttons
            float bottomY = bodyTop - bodyH - 10f;

            _readBtn = UIBuilder.CreateButton(content, "ReadBtn", "全部已读", 110, 38, 16,
                UIBuilder.ButtonNormal, UIBuilder.TextWhite);
            _readBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-170, bottomY);
            _readBtn.onClick.AddListener(OnReadAllClick);

            _claimBtn = UIBuilder.CreateButton(content, "ClaimBtn", "领取附件", 110, 38, 16,
                UIBuilder.ButtonNormal, UIBuilder.Gold);
            _claimBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-50, bottomY);
            _claimBtn.onClick.AddListener(OnClaimClick);

            _deleteBtn = UIBuilder.CreateButton(content, "DeleteBtn", "删除已读", 110, 38, 16,
                UIBuilder.ButtonNormal, new Color(0.8f, 0.3f, 0.3f));
            _deleteBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(70, bottomY);
            _deleteBtn.onClick.AddListener(OnDeleteReadClick);

            var closeBtn = UIBuilder.CreateButton(content, "CloseBtn", "关 闭", 110, 38, 16,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(190, bottomY);
            closeBtn.onClick.AddListener(OnCloseClick);

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            NetClient.Instance.Send(new QueryMailListReq());
        }

        public override void OnHide()
        {
        }

        // ==================== Handlers ====================

        public void HandleQueryMailListAck(QueryMailListAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }
            _mails = ack.Mails ?? new List<MailInfo>();
            _selectedMailId = -1;
            RefreshList();
            ClearDetail();
        }

        public void HandleMailReadAck(MailReadAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }
            if (ack.ReadMailIds != null)
            {
                foreach (var id in ack.ReadMailIds)
                {
                    var mail = _mails.Find(m => m.MailId == id);
                    if (mail != null) mail.State = 1;
                }
            }
            RefreshList();
            RefreshDetail();
            UIToast.Show("已标记为已读");
        }

        public void HandleMailGetItemAck(MailGetItemAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }
            if (ack.ClaimedMailIds != null)
            {
                foreach (var id in ack.ClaimedMailIds)
                {
                    var mail = _mails.Find(m => m.MailId == id);
                    if (mail != null) mail.State = 2;
                }
            }
            RefreshList();
            RefreshDetail();
            UIToast.Show("附件已领取");
        }

        public void HandleMailDeleteAck(MailDeleteAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }
            if (ack.DeletedMailIds != null)
            {
                _mails.RemoveAll(m => ack.DeletedMailIds.Contains(m.MailId));
            }
            if (_selectedMailId >= 0 && !_mails.Exists(m => m.MailId == _selectedMailId))
            {
                _selectedMailId = -1;
                ClearDetail();
            }
            RefreshList();
            UIToast.Show("邮件已删除");
        }

        public void HandleMailNewNtf(MailNewNtf ntf)
        {
            if (ntf.Mail != null)
            {
                _mails.Insert(0, ntf.Mail);
                RefreshList();
                UIToast.Show("收到新邮件: " + ntf.Mail.Title);
            }
        }

        // ==================== UI Refresh ====================

        private void RefreshList()
        {
            _mailCountText.text = _mails.Count + " 封";

            // Clear old items
            for (int i = _listContent.transform.childCount - 1; i >= 0; i--)
                Destroy(_listContent.transform.GetChild(i).gameObject);

            float totalH = _mails.Count * (MAIL_ITEM_H + MAIL_ITEM_GAP);
            var lcRect = _listContent.GetComponent<RectTransform>();
            lcRect.sizeDelta = new Vector2(0, totalH);

            for (int i = 0; i < _mails.Count; i++)
            {
                var mail = _mails[i];
                var item = CreateMailListItem(_listContent.transform, mail, i);
                var itemRect = item.GetComponent<RectTransform>();
                itemRect.anchoredPosition = new Vector2(0, -i * (MAIL_ITEM_H + MAIL_ITEM_GAP));
            }
        }

        private GameObject CreateMailListItem(Transform parent, MailInfo mail, int index)
        {
            bool isSelected = mail.MailId == _selectedMailId;
            bool isUnread = mail.State == 0;
            bool hasAttach = mail.Attachments != null && mail.Attachments.Count > 0 && mail.State < 2;

            var item = new GameObject("Mail_" + index);
            item.transform.SetParent(parent, false);
            var rect = item.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, MAIL_ITEM_H);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);

            var bg = item.AddComponent<Image>();
            bg.color = isSelected
                ? new Color(0.2f, 0.18f, 0.35f, 0.95f)
                : (isUnread ? new Color(0.1f, 0.1f, 0.22f, 0.95f) : new Color(0.07f, 0.07f, 0.14f, 0.9f));

            var btn = item.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            // Title text
            Color titleColor = isUnread ? UIBuilder.Gold : UIBuilder.TextGray;
            string titleText = (isUnread ? "[新] " : "") + mail.Title;
            var title = UIBuilder.CreateText(item.transform, "Title", titleText, 14,
                titleColor, TextAnchor.MiddleLeft, isUnread ? FontStyle.Bold : FontStyle.Normal);
            var tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0, 0.5f);
            tRect.anchorMax = new Vector2(1, 1);
            tRect.offsetMin = new Vector2(10, 0);
            tRect.offsetMax = new Vector2(hasAttach ? -30 : -10, -2);

            // Sender text
            var sender = UIBuilder.CreateText(item.transform, "Sender", mail.Sender, 12,
                UIBuilder.TextDim, TextAnchor.MiddleLeft);
            var sRect = sender.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 0);
            sRect.anchorMax = new Vector2(1, 0.5f);
            sRect.offsetMin = new Vector2(10, 2);
            sRect.offsetMax = new Vector2(-10, 0);

            // Attachment indicator
            if (hasAttach)
            {
                var attachIcon = UIBuilder.CreateText(item.transform, "AttachIcon", "*",
                    18, new Color(1f, 0.6f, 0.1f), TextAnchor.MiddleRight, FontStyle.Bold);
                var aRect = attachIcon.GetComponent<RectTransform>();
                aRect.anchorMin = new Vector2(1, 0);
                aRect.anchorMax = new Vector2(1, 1);
                aRect.pivot = new Vector2(1, 0.5f);
                aRect.sizeDelta = new Vector2(24, 24);
                aRect.anchoredPosition = new Vector2(-6, 0);
            }

            long mailId = mail.MailId;
            btn.onClick.AddListener(() => OnMailItemClick(mailId));

            return item;
        }

        private void OnMailItemClick(long mailId)
        {
            _selectedMailId = mailId;
            RefreshList();
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            var mail = _mails.Find(m => m.MailId == _selectedMailId);
            if (mail == null)
            {
                ClearDetail();
                return;
            }

            _detailTitle.text = mail.Title;
            _detailSender.text = "来自: " + mail.Sender;
            _detailContent.text = mail.Content;
            _detailTime.text = FormatTime(mail.SendTime);

            if (mail.Attachments != null && mail.Attachments.Count > 0)
            {
                var parts = new List<string>();
                foreach (var att in mail.Attachments)
                {
                    var cfg = ConfigManager.Get<ItemConfig>(att.ConfigId);
                    string name = cfg != null ? cfg.Name : ("物品#" + att.ConfigId);
                    parts.Add(name + " x" + att.Count);
                }
                string stateText = mail.State >= 2 ? " [已领取]" : " [待领取]";
                _detailAttachments.text = "附件: " + string.Join(", ", parts) + stateText;
                _detailAttachments.color = mail.State >= 2 ? UIBuilder.TextDim : UIBuilder.Gold;
            }
            else
            {
                _detailAttachments.text = "";
            }
        }

        private void ClearDetail()
        {
            _detailTitle.text = "选择一封邮件查看详情";
            _detailSender.text = "";
            _detailContent.text = "";
            _detailTime.text = "";
            _detailAttachments.text = "";
        }

        // ==================== Actions ====================

        private void OnReadAllClick()
        {
            var unreadIds = new List<long>();
            foreach (var m in _mails)
            {
                if (m.State == 0) unreadIds.Add(m.MailId);
            }
            if (unreadIds.Count == 0)
            {
                UIToast.Show("没有未读邮件");
                return;
            }
            NetClient.Instance.Send(new MailReadReq { MailIds = unreadIds });
        }

        private void OnClaimClick()
        {
            var claimIds = new List<long>();
            if (_selectedMailId >= 0)
            {
                var mail = _mails.Find(m => m.MailId == _selectedMailId);
                if (mail != null && mail.Attachments != null && mail.Attachments.Count > 0 && mail.State < 2)
                    claimIds.Add(mail.MailId);
            }
            // Also claim all other claimable mails
            foreach (var m in _mails)
            {
                if (m.State == 1 && m.Attachments != null && m.Attachments.Count > 0 && !claimIds.Contains(m.MailId))
                    claimIds.Add(m.MailId);
            }
            if (claimIds.Count == 0)
            {
                UIToast.Show("没有可领取的附件");
                return;
            }
            NetClient.Instance.Send(new MailGetItemReq { MailIds = claimIds });
        }

        private void OnDeleteReadClick()
        {
            var deleteIds = new List<long>();
            foreach (var m in _mails)
            {
                if (m.State >= 1)
                {
                    bool hasUnclaimed = m.Attachments != null && m.Attachments.Count > 0 && m.State < 2;
                    if (!hasUnclaimed) deleteIds.Add(m.MailId);
                }
            }
            if (deleteIds.Count == 0)
            {
                UIToast.Show("没有可删除的邮件");
                return;
            }
            NetClient.Instance.Send(new MailDeleteReq { MailIds = deleteIds });
        }

        private void OnCloseClick()
        {
            UIManager.Instance.Back();
        }

        // ==================== Utility ====================

        private static GameObject CreateSection(Transform parent, string name, float width, float height)
        {
            return UIBuilder.CreatePanel(parent, name, width, height, new Color(0.06f, 0.06f, 0.12f, 0.9f));
        }

        private static string FormatTime(long timestamp)
        {
            if (timestamp <= 0) return "";
            var dt = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            dt = dt.AddMilliseconds(timestamp).ToLocalTime();
            return dt.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
