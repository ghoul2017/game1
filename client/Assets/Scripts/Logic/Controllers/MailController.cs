using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class MailController
    {
        public static MailController Instance { get; } = new();

        public List<MailInfo> Mails { get; private set; } = new();
        public long SelectedMailId { get; private set; } = -1;

        public event Action OnDataChanged;

        public async Task QueryMailList()
        {
            try
            {
                QueryMailListAck ack = await NetClient.Instance.CallAsync<QueryMailListAck>(new QueryMailListReq());
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    Context.UI?.ShowToast(ack.ErrorMessage);
                    return;
                }
                Mails = ack.Mails ?? new List<MailInfo>();
                SelectedMailId = -1;
                OnDataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("查询邮件失败: " + ex.Message);
            }
        }

        public void SelectMail(long mailId)
        {
            SelectedMailId = mailId;
            OnDataChanged?.Invoke();
        }

        public async Task ReadAll()
        {
            var unreadIds = new List<long>();
            foreach (var m in Mails)
            {
                if (m.State == 0) unreadIds.Add(m.MailId);
            }
            if (unreadIds.Count == 0)
            {
                Context.UI?.ShowToast("没有未读邮件");
                return;
            }
            try
            {
                MailReadAck ack = await NetClient.Instance.CallAsync<MailReadAck>(
                    new MailReadReq { MailIds = unreadIds });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    Context.UI?.ShowToast(ack.ErrorMessage);
                    return;
                }
                if (ack.ReadMailIds != null)
                {
                    foreach (var id in ack.ReadMailIds)
                    {
                        var mail = Mails.Find(m => m.MailId == id);
                        if (mail != null) mail.State = 1;
                    }
                }
                OnDataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("读取邮件失败: " + ex.Message);
            }
        }

        public async Task ClaimAttachments()
        {
            var claimIds = new List<long>();
            if (SelectedMailId >= 0)
            {
                var mail = Mails.Find(m => m.MailId == SelectedMailId);
                if (mail != null && mail.Attachments != null && mail.Attachments.Count > 0 && mail.State < 2)
                    claimIds.Add(mail.MailId);
            }
            foreach (var m in Mails)
            {
                if (m.State == 1 && m.Attachments != null && m.Attachments.Count > 0 && !claimIds.Contains(m.MailId))
                    claimIds.Add(m.MailId);
            }
            if (claimIds.Count == 0)
            {
                Context.UI?.ShowToast("没有可领取的附件");
                return;
            }
            try
            {
                MailGetItemAck ack = await NetClient.Instance.CallAsync<MailGetItemAck>(
                    new MailGetItemReq { MailIds = claimIds });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    Context.UI?.ShowToast(ack.ErrorMessage);
                    return;
                }
                if (ack.ClaimedMailIds != null)
                {
                    foreach (var id in ack.ClaimedMailIds)
                    {
                        var mail = Mails.Find(m => m.MailId == id);
                        if (mail != null) mail.State = 2;
                    }
                }
                OnDataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("领取附件失败: " + ex.Message);
            }
        }

        public async Task DeleteRead()
        {
            var deleteIds = new List<long>();
            foreach (var m in Mails)
            {
                if (m.State >= 1)
                {
                    bool hasUnclaimed = m.Attachments != null && m.Attachments.Count > 0 && m.State < 2;
                    if (!hasUnclaimed) deleteIds.Add(m.MailId);
                }
            }
            if (deleteIds.Count == 0)
            {
                Context.UI?.ShowToast("没有可删除的邮件");
                return;
            }
            try
            {
                MailDeleteAck ack = await NetClient.Instance.CallAsync<MailDeleteAck>(
                    new MailDeleteReq { MailIds = deleteIds });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    Context.UI?.ShowToast(ack.ErrorMessage);
                    return;
                }
                if (ack.DeletedMailIds != null)
                {
                    Mails.RemoveAll(m => ack.DeletedMailIds.Contains(m.MailId));
                }
                if (SelectedMailId >= 0 && !Mails.Exists(m => m.MailId == SelectedMailId))
                {
                    SelectedMailId = -1;
                }
                OnDataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("删除邮件失败: " + ex.Message);
            }
        }

        public void HandleMailNewNtf(MailNewNtf ntf)
        {
            if (ntf.Mail != null)
            {
                Mails.Insert(0, ntf.Mail);
                OnDataChanged?.Invoke();
                Context.UI?.ShowToast("收到新邮件: " + ntf.Mail.Title);
            }
        }
    }
}
