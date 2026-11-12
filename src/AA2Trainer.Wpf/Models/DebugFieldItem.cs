namespace AA2Trainer.WpfPrototype
{
    public sealed class DebugFieldItem : ObservableObject
    {
        private string category = "";
        private string name = "";
        private string address = "";
        private string type = "";
        private string fieldValue = "";
        private string note = "";
        private string chain = "";
        private string status = "";
        private string trace = "";
        private string bytesText = "";
        private bool optional;
        private bool isProblem;
        private bool isPointer;

        public string Category
        {
            get { return category; }
            set { Set(ref category, value ?? ""); }
        }

        public string Name
        {
            get { return name; }
            set { Set(ref name, value ?? ""); }
        }

        public string Address
        {
            get { return address; }
            set { Set(ref address, value ?? ""); }
        }

        public string Type
        {
            get { return type; }
            set { Set(ref type, value ?? ""); }
        }

        public string Value
        {
            get { return fieldValue; }
            set { Set(ref fieldValue, value ?? ""); }
        }

        public string Note
        {
            get { return note; }
            set { Set(ref note, value ?? ""); }
        }

        public string Chain
        {
            get { return chain; }
            set { Set(ref chain, value ?? ""); }
        }

        public string Status
        {
            get { return status; }
            set { Set(ref status, value ?? ""); }
        }

        public string Trace
        {
            get { return trace; }
            set { Set(ref trace, value ?? ""); }
        }

        public string BytesText
        {
            get { return bytesText; }
            set { Set(ref bytesText, value ?? ""); }
        }

        public bool Optional
        {
            get { return optional; }
            set { Set(ref optional, value); }
        }

        public bool IsProblem
        {
            get { return isProblem; }
            set { Set(ref isProblem, value); }
        }

        public bool IsPointer
        {
            get { return isPointer; }
            set { Set(ref isPointer, value); }
        }

        public void Apply(DebugFieldItem source)
        {
            if (source == null) return;
            Category = source.Category;
            Name = source.Name;
            Address = source.Address;
            Type = source.Type;
            Value = source.Value;
            Note = source.Note;
            Chain = source.Chain;
            Status = source.Status;
            Trace = source.Trace;
            BytesText = source.BytesText;
            Optional = source.Optional;
            IsProblem = source.IsProblem;
            IsPointer = source.IsPointer;
        }
    }
}
