﻿CREATE TABLE [dbo].[TB_Chat] (
    [ChatroomID] UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID() NOT NULL,
    [ChatName]   NVARCHAR (50)    NOT NULL,
    [Content]    NVARCHAR (MAX)   NOT NULL,
    PRIMARY KEY CLUSTERED ([ChatroomID] ASC)
);
