﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace OpenAIAPI.Models
{
    public partial class TB_Texts
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid? FolderId { get; set; }
        public Guid? EmbeddingId { get; set; }
        public string TextContent { get; set; }
        public string TextHtml { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}