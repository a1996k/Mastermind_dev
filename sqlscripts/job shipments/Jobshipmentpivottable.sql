USE [FifthPress]
GO

/****** Object:  Table [dbo].[JobShipmentPivotTable]    Script Date: 3/1/2025 11:50:17 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[JobShipmentPivotTable](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Job] [int] NOT NULL,
	[Shipment] [int] NOT NULL,
 CONSTRAINT [PK_JobShipmentPivotTable] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[JobShipmentPivotTable]  WITH NOCHECK ADD  CONSTRAINT [FK_JobShipmentPivotTable_Jobs] FOREIGN KEY([Job])
REFERENCES [dbo].[Jobs] ([Id])
GO

ALTER TABLE [dbo].[JobShipmentPivotTable] CHECK CONSTRAINT [FK_JobShipmentPivotTable_Jobs]
GO

ALTER TABLE [dbo].[JobShipmentPivotTable]  WITH NOCHECK ADD  CONSTRAINT [FK_JobShipmentPivotTable_JobShipments] FOREIGN KEY([Shipment])
REFERENCES [dbo].[JobShipments] ([Id])
NOT FOR REPLICATION 
GO

ALTER TABLE [dbo].[JobShipmentPivotTable] NOCHECK CONSTRAINT [FK_JobShipmentPivotTable_JobShipments]
GO


