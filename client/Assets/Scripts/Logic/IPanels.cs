using Shared.Common.Protos;

namespace Client.Logic
{
    public interface ILoginPanel
    {
        void HandleLoginRes(LoginRes res);
    }

    public interface IRoleSelectPanel
    {
        void HandleCreateRoleRes(CreateRoleRes res);
        void HandleEnterGameRes(EnterGameRes res);
        void HandleEnterGameNtf(EnterGameNtf ntf);
        void HandleEnterMapAck(EnterMapAck ack);
    }

    public interface IBagPanel
    {
        void HandleQueryBagAck(QueryBagAck ack);
        void HandleBagTidyAck(BagTidyAck ack);
        void HandleBagUseItemAck(BagUseItemAck ack);
        void HandleBagItemChangedNtf(BagItemChangedNtf ntf);
    }

    public interface IMailPanel
    {
        void HandleQueryMailListAck(QueryMailListAck ack);
        void HandleMailReadAck(MailReadAck ack);
        void HandleMailGetItemAck(MailGetItemAck ack);
        void HandleMailDeleteAck(MailDeleteAck ack);
        void HandleMailNewNtf(MailNewNtf ntf);
    }

    public interface ITaskPanel
    {
        void HandleQueryTaskListAck(QueryTaskListAck ack);
        void HandleTaskAcceptAck(TaskAcceptAck ack);
        void HandleTaskSubmitAck(TaskSubmitAck ack);
        void HandleTaskGiveUpAck(TaskGiveUpAck ack);
        void HandleTaskChangedNtf(TaskChangedNtf ntf);
    }

    public interface IMainPanel
    {
        void HandleChatAck(ChatAck ack);
        void HandleChatNtf(ChatNtf ntf);
        void HandleSystemChatNtf(SystemChatNtf ntf);
        void HandleUseSkillAck(UseSkillAck ack);
        void HandleHpChangedNtf(HpChangedNtf ntf);
        void HandleKilledByNtf(KilledByNtf ntf);
        void HandlePkModeChangedNtf(PkModeChangedNtf ntf);
    }
}
