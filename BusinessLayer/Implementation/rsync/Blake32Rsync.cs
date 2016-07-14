using System.IO;

namespace BusinessLayer.Implementation.rsync
{
    public static class Blake32Rsync
    {
        public static Stream ComputeSignature(Stream inputFile)
        {
            return ComputeSignature(
                inputFile,
                new SignatureJobSettings
                {
                    MagicNumber = MagicNumber.Blake2Signature,
                    BlockLength = SignatureHelpers.DefaultBlockLength,
                    StrongSumLength = SignatureHelpers.DefaultStrongSumLength
                });
        }

        public static Stream ComputeSignature(Stream inputFile, SignatureJobSettings settings)
        {
            return new SignatureStream(inputFile, settings);
        }

        public static Stream ComputeDelta(Stream signature, Stream newFile)
        {
            return new DeltaStream(signature, newFile);
        }

        public static Stream ApplyDelta(Stream originalFile, Stream delta)
        {
            return new PatchedStream(originalFile, delta);
        }
    }
}