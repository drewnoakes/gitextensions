            else
                return GetPatchBytes(header, body, fileContentEncoding);
            string[] headerLines = header.Split(new string[] {"\n"}, StringSplitOptions.RemoveEmptyEntries);
            else
                return GetPatchBytes(header, body, fileContentEncoding);
            var s = new System.Text.StringBuilder();
            var x = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] bs = System.Text.Encoding.UTF8.GetBytes(input);
            var c = new PatchLine();
            c.Text = Text;
            c.Selected = Selected;
            return c;
        public string WasNoNewLineAtTheEnd = null;
        public string IsNoNewLineAtTheEnd = null;
        private List<SubChunk> SubChunks = new List<SubChunk>();
        private SubChunk _CurrentSubChunk = null;
                    PatchLine patchLine = new PatchLine()
            Chunk result = new Chunk();
            result.StartLine = 0;
            string[] lines = fileText.Split(new string[] { eol }, StringSplitOptions.None);
                string preamble = (i == 0 ? new string(fileContentEncoding.GetChars(FilePreabmle)) : string.Empty);
                PatchLine patchLine = new PatchLine()
            string[] chunks = diff.Split(new string[] { "\n@@" }, StringSplitOptions.RemoveEmptyEntries);
            Chunk chunk = Chunk.FromNewFile(module, text, selectionPosition, selectionLength, reset, FilePreabmle, fileContentEncoding);
            ChunkList result = new ChunkList();
            result.Add(chunk);
            return result;