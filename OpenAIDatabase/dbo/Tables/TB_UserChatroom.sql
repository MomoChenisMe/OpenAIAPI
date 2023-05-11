CREATE TABLE [dbo].[TB_UserChatroom] (
    [AccountEmail] NVARCHAR (50)    NOT NULL,
    [ChatroomID]   UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY CLUSTERED ([AccountEmail] ASC, [ChatroomID] ASC)
);

