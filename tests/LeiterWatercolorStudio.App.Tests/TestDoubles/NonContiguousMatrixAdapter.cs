using Leiter.Core;
using Leiter.Pixels;

namespace LeiterWatercolorStudio.App.Tests.TestDoubles;

internal sealed class NonContiguousMatrixAdapter(SequentialMatrix<Rgb8> inner) : Matrix<Rgb8>(inner.Width, inner.Height)
{
    public override Rgb8 GetElement(int index) => inner.GetElement(index);
    public override Rgb8 GetElement(int x, int y) => inner.GetElement(x, y);
    public override void SetElement(int index, Rgb8 value) => inner.SetElement(index, value);
    public override void SetElement(int x, int y, Rgb8 value) => inner.SetElement(x, y, value);
    public override void SetAll(Rgb8 value) => inner.SetAll(value);
    public override Matrix<R> Map<R>(Func<Rgb8, R> func) => inner.Map(func);
    public override Matrix<Rgb8> Clone() => inner.Clone();
}
