using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EsellerDbContext))]
[Migration("20261005070000_AddSupportConversationsAndMessages")]
public partial class AddSupportConversationsAndMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Conversations]', N'U') IS NULL
BEGIN
    CREATE TABLE [Conversations] (
        [Id] uniqueidentifier NOT NULL,
        [ParticipantId] uniqueidentifier NOT NULL,
        [ParticipantType] int NOT NULL,
        [LastMessageAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [UX_Conversations_Participant] ON [Conversations] ([ParticipantId], [ParticipantType]);
    CREATE INDEX [IX_Conversations_LastMessageAt] ON [Conversations] ([LastMessageAt]);
END
");

        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Messages]', N'U') IS NULL
BEGIN
    CREATE TABLE [Messages] (
        [Id] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [SenderId] uniqueidentifier NOT NULL,
        [SenderRole] int NOT NULL,
        [Content] nvarchar(4000) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [IsRead] bit NOT NULL CONSTRAINT [DF_Messages_IsRead] DEFAULT CAST(0 AS bit),
        [ReadAt] datetime2 NULL,
        [AttachmentUrl] nvarchar(1000) NULL,
        [AttachmentFileName] nvarchar(260) NULL,
        [AttachmentContentType] nvarchar(200) NULL,
        [AttachmentSizeBytes] bigint NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Messages_Accounts_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_Messages_ConversationId] ON [Messages] ([ConversationId]);
    CREATE INDEX [IX_Messages_ConversationId_IsRead] ON [Messages] ([ConversationId], [IsRead]);
    CREATE INDEX [IX_Messages_SenderId] ON [Messages] ([SenderId]);
    CREATE INDEX [IX_Messages_SentAt] ON [Messages] ([SentAt]);
END
");

        migrationBuilder.Sql(@"
INSERT INTO Conversations (Id, ParticipantId, ParticipantType, LastMessageAt, CreatedAt, UpdatedAt)
SELECT s.Id, s.Id, 2, MAX(cm.SentAt), MIN(COALESCE(s.CreatedAt, GETUTCDATE())), GETUTCDATE()
FROM Shops s
INNER JOIN ChatMessages cm ON cm.OrderRequestId = s.Id
WHERE s.DeletedAt IS NULL
  AND NOT EXISTS (SELECT 1 FROM Conversations c WHERE c.Id = s.Id)
GROUP BY s.Id;
");

        migrationBuilder.Sql(@"
INSERT INTO Messages (Id, ConversationId, SenderId, SenderRole, Content, SentAt, IsRead, ReadAt, AttachmentUrl, AttachmentFileName, AttachmentContentType, AttachmentSizeBytes)
SELECT cm.Id, cm.OrderRequestId, cm.SenderAccountId, CAST(cm.SenderRole AS int), cm.Message, cm.SentAt, cm.IsRead, cm.ReadAt, NULL, NULL, NULL, NULL
FROM ChatMessages cm
INNER JOIN Conversations c ON c.Id = cm.OrderRequestId AND c.ParticipantType = 2
WHERE NOT EXISTS (SELECT 1 FROM Messages m WHERE m.Id = cm.Id);
");

        migrationBuilder.Sql(@"
INSERT INTO Conversations (Id, ParticipantId, ParticipantType, LastMessageAt, CreatedAt, UpdatedAt)
SELECT x.UserId, x.UserId, 1, x.LastAt, x.CreatedAt, GETUTCDATE()
FROM (
    SELECT o.UserId AS UserId, MAX(cm.SentAt) AS LastAt, MIN(COALESCE(o.CreatedAt, GETUTCDATE())) AS CreatedAt
    FROM OrderRequests o
    INNER JOIN ChatMessages cm ON cm.OrderRequestId = o.Id
    WHERE o.UserId <> '00000000-0000-0000-0000-000000000000'
      AND (o.ShippingAddress LIKE 'CUSTOMER_SUPPORT_%' OR o.ShippingAddress = 'Live Support Inquiry')
    GROUP BY o.UserId
) x
WHERE NOT EXISTS (SELECT 1 FROM Conversations c WHERE c.Id = x.UserId);
");

        migrationBuilder.Sql(@"
INSERT INTO Messages (Id, ConversationId, SenderId, SenderRole, Content, SentAt, IsRead, ReadAt, AttachmentUrl, AttachmentFileName, AttachmentContentType, AttachmentSizeBytes)
SELECT cm.Id, o.UserId, cm.SenderAccountId, CAST(cm.SenderRole AS int), cm.Message, cm.SentAt, cm.IsRead, cm.ReadAt, NULL, NULL, NULL, NULL
FROM ChatMessages cm
INNER JOIN OrderRequests o ON o.Id = cm.OrderRequestId
INNER JOIN Conversations c ON c.Id = o.UserId AND c.ParticipantType = 1
WHERE (o.ShippingAddress LIKE 'CUSTOMER_SUPPORT_%' OR o.ShippingAddress = 'Live Support Inquiry')
  AND NOT EXISTS (SELECT 1 FROM Messages m WHERE m.Id = cm.Id);
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF OBJECT_ID(N'[Messages]', N'U') IS NOT NULL DROP TABLE [Messages];");
        migrationBuilder.Sql("IF OBJECT_ID(N'[Conversations]', N'U') IS NOT NULL DROP TABLE [Conversations];");
    }
}
