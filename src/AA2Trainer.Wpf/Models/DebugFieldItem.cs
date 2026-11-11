namespace AA2Trainer.WpfPrototype
{
    public sealed class DebugFieldItem : ObservableObject
    {
        private string name = "";
        private string address = "";
        private string type = "";
        private string fieldValue = "";
        private string note = "";

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

        public void Apply(DebugFieldItem source)
        {
            if (source == null) return;
            Name = source.Name;
            Address = source.Address;
            Type = source.Type;
            Value = source.Value;
            Note = source.Note;
        }
    }
}
