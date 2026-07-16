using System.Security.Cryptography;
using iText.Bouncycastleconnector;
using iText.Commons.Bouncycastle;
using iText.Commons.Bouncycastle.Cert;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Signatures;

namespace Application.Services.Signing
{
    /// <summary>
    /// Nhung chu ky mat ma that (CAdES/PAdES) vao PDF bang ket qua ky cua 1 dich vu ky so ngoai (VNPT SmartCA),
    /// dung mo hinh ky 2 pha cua iText (PdfTwoPhaseSigner): chuan bi hash truoc (Phase 1, luc gui yeu cau ky),
    /// roi nhung chu ky that vao sau khi VNPT tra ve ket qua (Phase 2, luc poll trang thai giao dich xong).
    ///
    /// Ghi chu: data thuc su duoc bam va gui cho VNPT ky la SHA-256 cua "authenticated attributes" (CAdES),
    /// KHONG PHAI hash truc tiep cua PDF - day la chuan CAdES-BES/PAdES-BES pho bien cho remote/cloud signing.
    /// </summary>
    public static class PdfExternalSignatureHelper
    {
        private const string DigestAlgorithm = "SHA256";
        private const int EstimatedSignatureSize = 32_000; // du cho CMS + chain 2-3 chung thu

        public sealed record PreparedSignature(
            byte[] PreparedPdfBytes,
            byte[] DocumentDigest,
            byte[] SignedAttributes,
            byte[] HashToSign,
            string FieldName);

        /// <summary>
        /// Phase 1: dat cho signature field (an, khong hien thi widget vi khung "CHU KY SO" da duoc ve
        /// truc quan truoc do len noi dung trang bang PdfSignatureService.DrawSignatureStamp), tinh document
        /// digest, dung PdfPKCS7 de dung "authenticated attributes" (CAdES) roi bam SHA-256 ra hash can gui
        /// cho VNPT ky.
        /// </summary>
        public static PreparedSignature PrepareForSigning(byte[] stampedPdfBytes, byte[] signerCertDer)
        {
            var factory = BouncyCastleFactoryCreator.GetFactory();
            var certChain = new[] { WrapCertificate(factory, signerCertDer) };

            using var inputStream = new MemoryStream(stampedPdfBytes);
            using var outputStream = new MemoryStream();
            var reader = new PdfReader(inputStream);
            var fieldName = $"VnptSmartCaSig_{Guid.NewGuid():N}";

            var signerProperties = new SignerProperties()
                .SetFieldName(fieldName)
                .SetPageNumber(1)
                .SetPageRect(new Rectangle(0, 0, 0, 0)); // chu ky an - khung truc quan da ve rieng roi

            var twoPhaseSigner = new PdfTwoPhaseSigner(reader, outputStream);
            var documentDigest = twoPhaseSigner.PrepareDocumentForSignature(
                signerProperties,
                DigestAlgorithm,
                PdfName.Adobe_PPKLite,
                PdfName.ETSI_CAdES_DETACHED,
                EstimatedSignatureSize,
                includeDate: true);

            var pkcs7 = new PdfPKCS7(null, certChain, DigestAlgorithm, null, false);
            var signedAttributes = pkcs7.GetAuthenticatedAttributeBytes(
                documentDigest, PdfSigner.CryptoStandard.CMS, null, null);
            var hashToSign = SHA256.HashData(signedAttributes);

            return new PreparedSignature(
                outputStream.ToArray(),
                documentDigest,
                signedAttributes,
                hashToSign,
                fieldName);
        }

        /// <summary>
        /// Phase 2: nhan chu ky tho (raw RSA signature) VNPT tra ve cho hash da gui o Phase 1, dung lai
        /// cung authenticated attributes de dung CMS/PKCS7 hoan chinh roi nhung vao vi tri da dat cho.
        /// </summary>
        public static byte[] CompleteSigning(
            byte[] preparedPdfBytes,
            byte[] documentDigest,
            byte[] signedAttributes,
            byte[] rawSignatureFromVnpt,
            byte[] signerCertDer)
        {
            var fieldName = FindBlankSignatureFieldName(preparedPdfBytes);

            byte[] encodedPkcs7;
            if (LooksLikeCompleteCms(rawSignatureFromVnpt))
            {
                // VNPT da tra ve CMS/PKCS7 SignedData hoan chinh (khong phai raw signature don) - dung thang.
                encodedPkcs7 = rawSignatureFromVnpt;
            }
            else
            {
                var factory = BouncyCastleFactoryCreator.GetFactory();
                var certChain = new[] { WrapCertificate(factory, signerCertDer) };

                var pkcs7 = new PdfPKCS7(null, certChain, DigestAlgorithm, null, false);
                pkcs7.SetExternalSignatureValue(rawSignatureFromVnpt, signedAttributes, "RSA");
                encodedPkcs7 = pkcs7.GetEncodedPKCS7(
                    documentDigest, PdfSigner.CryptoStandard.CMS, null, null, null);
            }

            using var readerStream = new MemoryStream(preparedPdfBytes);
            using var outputStream = new MemoryStream();
            var reader = new PdfReader(readerStream);
            using (var document = new PdfDocument(reader))
            {
                PdfTwoPhaseSigner.AddSignatureToPreparedDocument(document, fieldName, outputStream, encodedPkcs7);
            }

            return outputStream.ToArray();
        }

        /// <summary>Neu VNPT da tra ve CMS/PKCS7 hoan chinh (khong phai raw signature) - byte dau la ASN.1 SEQUENCE (0x30).</summary>
        public static bool LooksLikeCompleteCms(byte[] signatureBytes)
            => signatureBytes.Length > 2 && signatureBytes[0] == 0x30;

        /// <summary>
        /// Doc ten field chu ky duy nhat trong PDF da chuan bi tu Phase 1. Luu y: SAU KHI
        /// PrepareDocumentForSignature, field da co /V (placeholder /Contents dat cho) nen KHONG con la
        /// "blank" theo dinh nghia cua SignatureUtil.GetBlankSignatureNames() (chi tinh field hoan toan
        /// chua co /V) - phai dung GetSignatureNames() de lay dung field vua tao.
        /// </summary>
        private static string FindBlankSignatureFieldName(byte[] preparedPdfBytes)
        {
            using var stream = new MemoryStream(preparedPdfBytes);
            var reader = new PdfReader(stream);
            using var document = new PdfDocument(reader);
            var signatureUtil = new SignatureUtil(document);
            var names = signatureUtil.GetSignatureNames();
            if (names.Count == 0)
                throw new InvalidOperationException("Prepared PDF has no pending signature field.");
            return names[names.Count - 1];
        }

        private static IX509Certificate WrapCertificate(IBouncyCastleFactory factory, byte[] certDer)
        {
            using var certStream = new MemoryStream(certDer);
            return factory.CreateX509Certificate(certStream);
        }
    }
}
