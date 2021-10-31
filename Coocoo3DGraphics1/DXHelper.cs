using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    unsafe struct D3D12_MEMCPY_DEST
    {
        public void* pData;
        public ulong RowPitch;
        public ulong SlicePitch;
    }

    public static class DXHelper
    {
        public static void ThrowIfFailed(SharpGen.Runtime.Result hr)
        {
            if (hr != SharpGen.Runtime.Result.Ok)
                throw new NotImplementedException(hr.ToString());
        }

        public static void memcpy<T>(Span<T> t1, Span<T> t2, int size) where T : unmanaged
        {
            int d1 = Marshal.SizeOf(typeof(T));
            t1.Slice(0, size / d1).CopyTo(t2);
        }

        unsafe public static void memcpy<T>(Span<T> t2, void* p1, int size) where T : unmanaged
        {
            int d1 = Marshal.SizeOf(typeof(T));
            new Span<T>(p1, size / d1).CopyTo(t2);
        }

        unsafe public static void memcpy<T>(T[] t2, void* p1, int size) where T : unmanaged
        {
            int d1 = Marshal.SizeOf(typeof(T));
            new Span<T>(p1, size / d1).CopyTo(t2);
        }

        unsafe public static void memcpy<T>(void* p1, Span<T> t2, int size) where T : unmanaged
        {
            int d1 = Marshal.SizeOf(typeof(T));
            t2.CopyTo(new Span<T>(p1, size / d1));
        }

        unsafe public static void memcpy<T>(void* p1, T[] t2, int size) where T : unmanaged
        {
            int d1 = Marshal.SizeOf(typeof(T));
            t2.CopyTo(new Span<T>(p1, size / d1));
        }

        public static float ConvertDipsToPixels(float dips, float dpi)
        {
            const float dipsPerInch = 96.0f;
            return (float)Math.Floor(dips * dpi / dipsPerInch + 0.5f); // 舍入到最接近的整数。
        }

        unsafe static void MemcpySubresource(
            D3D12_MEMCPY_DEST* pDest,
            SubresourceData pSrc,
            int RowSizeInBytes,
            int NumRows,
            int NumSlices)
        {
            for (uint z = 0; z < NumSlices; ++z)
            {
                byte* pDestSlice = (byte*)(pDest->pData) + pDest->SlicePitch * z;
                byte* pSrcSlice = (byte*)(pSrc.DataPointer) + (long)pSrc.SlicePitch * z;
                for (int y = 0; y < NumRows; ++y)
                {
                    new Span<byte>(pSrcSlice + ((long)pSrc.RowPitch * y), RowSizeInBytes).CopyTo(new Span<byte>(pDestSlice + (long)pDest->RowPitch * y, RowSizeInBytes));
                }
            }
        }

        public unsafe static ulong UpdateSubresources(
            ID3D12GraphicsCommandList pCmdList,
            ID3D12Resource pDestinationResource,
            ID3D12Resource pIntermediate,
            int FirstSubresource,
            int NumSubresources,
            ulong RequiredSize,
            PlacedSubresourceFootPrint[] pLayouts,
            int[] pNumRows,
            ulong[] pRowSizesInBytes,
            SubresourceData[] pSrcData)
        {
            var IntermediateDesc = pIntermediate.Description;
            var DestinationDesc = pDestinationResource.Description;
            if (IntermediateDesc.Dimension != ResourceDimension.Buffer ||
                IntermediateDesc.Width < RequiredSize + pLayouts[0].Offset ||
                (DestinationDesc.Dimension == ResourceDimension.Buffer &&
                    (FirstSubresource != 0 || NumSubresources != 1)))
            {
                return 0;
            }

            byte* pData;
            IntPtr data1 = pIntermediate.Map(0, null);
            pData = (byte*)data1;

            for (uint i = 0; i < NumSubresources; ++i)
            {
                D3D12_MEMCPY_DEST DestData = new D3D12_MEMCPY_DEST { pData = pData + pLayouts[i].Offset, RowPitch = (ulong)pLayouts[i].Footprint.RowPitch, SlicePitch = (uint)(pLayouts[i].Footprint.RowPitch) * (uint)(pNumRows[i]) };
                MemcpySubresource(&DestData, pSrcData[i], (int)(pRowSizesInBytes[i]), pNumRows[i], pLayouts[i].Footprint.Depth);
            }
            pIntermediate.Unmap(0, null);

            if (DestinationDesc.Dimension == ResourceDimension.Buffer)
            {
                pCmdList.CopyBufferRegion(
                    pDestinationResource, 0, pIntermediate, pLayouts[0].Offset, (ulong)pLayouts[0].Footprint.Width);
            }
            else
            {
                for (int i = 0; i < NumSubresources; ++i)
                {
                    TextureCopyLocation Dst = new TextureCopyLocation(pDestinationResource, i + FirstSubresource);
                    TextureCopyLocation Src = new TextureCopyLocation(pIntermediate, pLayouts[i]);
                    pCmdList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
                }
            }
            return RequiredSize;
        }

        public static ulong UpdateSubresources(
            ID3D12GraphicsCommandList pCmdList,
            ID3D12Resource pDestinationResource,
            ID3D12Resource pIntermediate,
            ulong IntermediateOffset,
            int FirstSubresource,
            int NumSubresources,
            SubresourceData[] pSrcData)
        {
            PlacedSubresourceFootPrint[] pLayouts = new PlacedSubresourceFootPrint[NumSubresources];
            ulong[] pRowSizesInBytes = new ulong[NumSubresources];
            int[] pNumRows = new int[NumSubresources];

            var Desc = pDestinationResource.Description;
            ID3D12Device pDevice = null;
            pDestinationResource.GetDevice(out pDevice);
            pDevice.GetCopyableFootprints(Desc, (int)FirstSubresource, (int)NumSubresources, IntermediateOffset, pLayouts, pNumRows, pRowSizesInBytes, out ulong RequiredSize);
            pDevice.Release();

            ulong Result = UpdateSubresources(pCmdList, pDestinationResource, pIntermediate, FirstSubresource, NumSubresources, RequiredSize, pLayouts, pNumRows, pRowSizesInBytes, pSrcData);
            return Result;
        }
    }
}
