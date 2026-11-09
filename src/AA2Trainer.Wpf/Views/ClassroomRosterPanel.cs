using System;
using System.Windows;
using System.Windows.Controls;

namespace AA2Trainer.WpfPrototype.Views
{
    /// <summary>
    /// Fixed 5x5 classroom layout. Seat numbering runs right-to-left by
    /// column, top-to-bottom, with the teacher seat (24) at the front-middle.
    /// The table matches the WinForms ClassRosterControl.SeatGrid.
    /// </summary>
    public sealed class ClassroomRosterPanel : Panel
    {
        private const int Columns = 5;
        private const int Rows = 5;

        private static readonly int[,] SeatGrid =
        {
            { 19, 14, 24, 5, 0 },
            { 20, 15, 10, 6, 1 },
            { 21, 16, 11, 7, 2 },
            { 22, 17, 12, 8, 3 },
            { 23, 18, 13, 9, 4 }
        };

        private static readonly int[] RowBySeat = new int[25];
        private static readonly int[] ColumnBySeat = new int[25];

        static ClassroomRosterPanel()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int seat = SeatGrid[row, column];
                    RowBySeat[seat] = row;
                    ColumnBySeat[seat] = column;
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = availableSize.Width;
            double height = availableSize.Height;
            if (double.IsInfinity(width) || double.IsNaN(width))
                width = Columns * 160;
            if (double.IsInfinity(height) || double.IsNaN(height))
                height = Rows * 120;

            Size cell = new Size(width / Columns, height / Rows);
            for (int i = 0; i < InternalChildren.Count; i++)
                InternalChildren[i].Measure(cell);

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double cellWidth = finalSize.Width / Columns;
            double cellHeight = finalSize.Height / Rows;

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                int seat = ResolveSeat(InternalChildren[i], i);
                int row = RowBySeat[seat];
                int column = ColumnBySeat[seat];
                InternalChildren[i].Arrange(new Rect(
                    column * cellWidth, row * cellHeight,
                    cellWidth, cellHeight));
            }

            return finalSize;
        }

        public static bool TryGetPosition(int seat, out int row, out int column)
        {
            if (seat < 0 || seat > 24)
            {
                row = -1;
                column = -1;
                return false;
            }

            row = RowBySeat[seat];
            column = ColumnBySeat[seat];
            return true;
        }

        public static int SeatAt(int row, int column)
        {
            if (row < 0 || row >= Rows || column < 0 || column >= Columns)
                return -1;
            return SeatGrid[row, column];
        }

        private static int ResolveSeat(UIElement child, int fallback)
        {
            FrameworkElement element = child as FrameworkElement;
            if (element != null)
            {
                SeatItem item = element.DataContext as SeatItem;
                if (item != null && item.SeatIndex >= 0 && item.SeatIndex < 25)
                    return item.SeatIndex;
            }

            // Keep a sane grid position if a container appears before its
            // data context is assigned.
            return fallback >= 0 && fallback < 25 ? fallback : 0;
        }
    }
}
