CREATE TABLE [dbo].[TB_User] (
    [AccountEmail]    NVARCHAR (100)  NOT NULL,
    [AccountPassword] NVARCHAR (1000) NOT NULL,
    [UserName]        NVARCHAR (50)   NOT NULL,
    [LoginType]       NVARCHAR (50)   NOT NULL,
    [HeadshotUrl] NVARCHAR(1000) NOT NULL, 
    CONSTRAINT [PK_TB_User1] PRIMARY KEY CLUSTERED ([AccountEmail] ASC)
);

