﻿using iText.Bouncycastle.Asn1.Tsp;
using iText.Bouncycastle.Cert;
using iText.Bouncycastle.X509;
using iText.Commons;
using iText.Commons.Bouncycastle.Asn1.Ocsp;
using iText.Commons.Bouncycastle.Cert;
using iText.Forms.Fields;
using iText.Forms;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Signatures;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Security.Certificates;
using Org.BouncyCastle.X509;

namespace DeCrypto
{
    public class C5_02_SignatureInfo
    {
        public PDFSignatureInfo PDFSignatureInfo { get; set; } = new PDFSignatureInfo();

        public SignaturePermissions InspectSignature(PdfDocument pdfDoc, SignatureUtil signUtil, PdfAcroForm form,
            String name, SignaturePermissions perms)
        {
            IList<PdfWidgetAnnotation> widgets = form.GetField(name).GetWidgets();

            if (widgets != null && widgets.Count > 0)
            {
                iText.Kernel.Geom.Rectangle pos = widgets[0].GetRectangle().ToRectangle();
                int pageNum = pdfDoc.GetPageNumber(widgets[0].GetPage());

                if (pos.GetWidth() == 0 || pos.GetHeight() == 0)
                {
                    //Console.Out.WriteLine("Invisible signature");
                }
                else
                {
                    //Console.Out.WriteLine(String.Format("Field on page {0}; llx: {1}, lly: {2}, urx: {3}; ury: {4}",
                    //    pageNum, pos.GetLeft(), pos.GetBottom(), pos.GetRight(), pos.GetTop()));
                }
            }

            /* Find out how the message digest of the PDF bytes was created,
             * how these bytes and additional attributes were signed
             * and how the signed bytes are stored in the PDF
             */
            PdfPKCS7 pkcs7 = VerifySignature(signUtil, name);
            //Console.Out.WriteLine("Digest algorithm: " + pkcs7.GetDigestAlgorithmName());
            //Console.Out.WriteLine("Encryption algorithm: " + pkcs7.GetSignatureAlgorithmName());
            //Console.Out.WriteLine("Filter subtype: " + pkcs7.GetFilterSubtype());

            // Get the signing certificate to find out the name of the signer.
            IX509Certificate cert = pkcs7.GetSigningCertificate();
            //Console.Out.WriteLine("Name of the signer: "
            //                      + iText.Signatures.CertificateInfo.GetSubjectFields(cert).GetField("CN"));
            if (pkcs7.GetSignName() != null)
            {
                //Console.Out.WriteLine("Alternative name of the signer: " + pkcs7.GetSignName());
            }

            /* Get the signing time.
             * Mind that the getSignDate() method is not that secure as timestamp
             * because it's based only on signature author claim. I.e. this value can only be trusted
             * if signature is trusted and it cannot be used for signature verification.
             */
            Console.Out.WriteLine("Signed on: " + pkcs7.GetSignDate().ToUniversalTime().ToString("yyyy-MM-dd HH:mm"));

            /* If a timestamp was applied, retrieve information about it.
             * Timestamp is a secure source of signature creation time,
             * because it's based on Time Stamping Authority service.
             */
            if (TimestampConstants.UNDEFINED_TIMESTAMP_DATE != pkcs7.GetTimeStampDate())
            {
                //Console.Out.WriteLine("TimeStamp: " +
                //                      pkcs7.GetTimeStampDate().ToUniversalTime().ToString("yyyy-MM-dd"));
                TstInfoBC ts = (TstInfoBC)pkcs7.GetTimeStampTokenInfo();
                //Console.Out.WriteLine("TimeStamp service: " + ts.GetTstInfo().Tsa);
                //Console.Out.WriteLine("Timestamp verified? " + pkcs7.VerifyTimestampImprint());
            }

            //Console.Out.WriteLine("Location: " + pkcs7.GetLocation());
            //Console.Out.WriteLine("Reason: " + pkcs7.GetReason());

            /* If you want less common entries than PdfPKCS7 object has, such as the contact info,
             * you should use the signature dictionary and get the properties by name.
             */
            PdfDictionary sigDict = signUtil.GetSignatureDictionary(name);
            PdfString contact = sigDict.GetAsString(PdfName.ContactInfo);
            if (contact != null)
            {
                //Console.Out.WriteLine("Contact info: " + contact);
            }

            /* Every new signature can add more restrictions to a document, but it can’t take away previous restrictions.
             * So if you want to retrieve information about signatures restrictions, you need to pass
             * the SignaturePermissions instance of the previous signature, or null if there was none.
             */
            perms = new SignaturePermissions(sigDict, perms);
            //Console.Out.WriteLine("Signature type: " + (perms.IsCertification() ? "certification" : "approval"));
            //Console.Out.WriteLine("Filling out fields allowed: " + perms.IsFillInAllowed());
            //Console.Out.WriteLine("Adding annotations allowed: " + perms.IsAnnotationsAllowed());
            foreach (SignaturePermissions.FieldLock Lock in perms.GetFieldLocks())
            {
                //Console.Out.WriteLine("Lock: " + Lock);
            }

            return perms;
        }

        public PdfPKCS7 VerifySignature(SignatureUtil signUtil, String name)
        {
            PdfPKCS7 pkcs7 = signUtil.ReadSignatureData(name);

            PDFSignatureInfo = new PDFSignatureInfo
            {
                SignatureCoverWholeDoc = signUtil.SignatureCoversWholeDocument(name),
                DocRevision = signUtil.GetRevision(name),
                DocTotalRevisions = signUtil.GetTotalRevisions(),
                IntegrityCheck = pkcs7.VerifySignatureIntegrityAndAuthenticity(),
                DigestAlgorithm = pkcs7.GetDigestAlgorithmName(),
                EncryptionAlgorithm = pkcs7.GetSignatureAlgorithmName(),
                Signer = iText.Signatures.CertificateInfo.GetSubjectFields(pkcs7.GetSigningCertificate()).GetField("CN"),
                Date = pkcs7.GetSignDate().ToUniversalTime().ToString("yyyy-MM-dd HH:mm"),
                Location = pkcs7.GetLocation(),
                Reason = pkcs7.GetReason()
                //Contact = pkcs7.GetContactInfo()
            };

            //Console.Out.WriteLine("Signature covers whole document: " + signUtil.SignatureCoversWholeDocument(name));
            //Console.Out.WriteLine("Document revision: " + signUtil.GetRevision(name) + " of "
            //                      + signUtil.GetTotalRevisions());
            ///Console.Out.WriteLine("Integrity check OK? " + pkcs7.VerifySignatureIntegrityAndAuthenticity());
            return pkcs7;
        }

        public virtual void InspectSignatures(String path)
        {
            PdfDocument pdfDoc = new PdfDocument(new PdfReader(path));
            PdfAcroForm form = PdfFormCreator.GetAcroForm(pdfDoc, false);
            SignaturePermissions perms = null;
            SignatureUtil signUtil = new SignatureUtil(pdfDoc);
            IList<String> names = signUtil.GetSignatureNames();

            foreach (String name in names)
            {
                //Console.Out.WriteLine("===== " + name + " =====");
                perms = InspectSignature(pdfDoc, signUtil, form, name, perms);
            }
            pdfDoc.Close();
        }

        //public static void Main(String[] args)
        //{
        //    C5_02_SignatureInfo app = new C5_02_SignatureInfo();
        //    app.InspectSignatures("");
        //}
    }
}