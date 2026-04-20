using HaCreator.MapSimulator.Fields;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class ClientOwnedFieldWrapperParityTests
{
    [Fact]
    public void LimitedViewMaskFocusPlan_IsEmptyWhenLocalFocusIsMissing()
    {
        var field = new LimitedViewField();
        field.EnableClientOwnedCircleMask(
            radius: 158f,
            width: 316f,
            height: 316f,
            originX: 158f,
            originY: 179f);
        field.SetClientOwnedFocusWorldPosition(120f, 240f);
        field.SetClientOwnedRemoteFocusWorldPositions(new[] { new Vector2(40f, 80f) });

        field.ClearClientOwnedFocusWorldPosition();

        Assert.Empty(field.GetClientOwnedUpdateParityMaskTopLefts(0, 0, 0, 0));
        Assert.Empty(field.GetClientOwnedUpdateParityScreenMaskCenters(0, 0, 0, 0));
    }

    [Fact]
    public void LimitedViewDrawViewrangePlan_KeepsClearThenReappendOrdering()
    {
        IReadOnlyList<LimitedViewField.ClientOwnedDrawViewrangeOperation> operations =
            LimitedViewField.BuildClientOwnedDrawViewrangeOperationPlan(
                previousMaskTopLefts: new[]
                {
                    new Vector2(100f, 200f),
                    new Vector2(300f, 400f)
                },
                currentMaskTopLefts: new[]
                {
                    new Vector2(110f, 210f),
                    new Vector2(310f, 410f)
                },
                viewrangeWidth: 316,
                viewrangeHeight: 316);

        Assert.Collection(
            operations,
            op => Assert.Equal(LimitedViewField.ClientOwnedDrawViewrangeOperationKind.RestorePreviousSmallDarkPatch, op.Kind),
            op => Assert.Equal(LimitedViewField.ClientOwnedDrawViewrangeOperationKind.RestorePreviousSmallDarkPatch, op.Kind),
            op => Assert.Equal(LimitedViewField.ClientOwnedDrawViewrangeOperationKind.ClearPreviousMaskHistory, op.Kind),
            op => Assert.Equal(LimitedViewField.ClientOwnedDrawViewrangeOperationKind.CopyLocalViewrange, op.Kind),
            op => Assert.Equal(LimitedViewField.ClientOwnedDrawViewrangeOperationKind.AppendPreviousMaskHistory, op.Kind),
            op => Assert.Equal(LimitedViewField.ClientOwnedDrawViewrangeOperationKind.CopyRemoteViewrange, op.Kind),
            op => Assert.Equal(LimitedViewField.ClientOwnedDrawViewrangeOperationKind.AppendPreviousMaskHistory, op.Kind));
    }
}
