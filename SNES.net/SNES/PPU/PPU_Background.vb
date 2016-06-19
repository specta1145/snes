﻿Partial Public Class PPU
    Dim BPPLUT(,) As Integer =
    {
        {2, 2, 2, 2},
        {4, 4, 2, 0},
        {4, 4, 0, 0},
        {8, 4, 0, 0},
        {8, 2, 0, 0},
        {4, 2, 0, 0},
        {4, 0, 0, 0},
        {8, 8, 0, 0}
    }

    Public Sub RenderLayer(Line As Integer, Layer As Integer, Optional Fg As Boolean = False)
        If (TM Or TS) And (1 << Layer) Then
            Dim Mode As Integer = BgMode And 7
            Dim BPP As Integer = BPPLUT(Mode, Layer)

            Dim BPPLSh As Integer

            Select Case BPP
                Case 2 : BPPLSh = 4
                Case 4 : BPPLSh = 5
                Case 8 : BPPLSh = 6
            End Select

            With Bg(Layer)
                If Mode = 7 Then
                    Dim Offset As Integer = 0
                    Dim ScrnOver As Integer = M7Sel >> 6

                    Dim A As Integer = Sign16(M7A)
                    Dim B As Integer = Sign16(M7B)
                    Dim C As Integer = Sign16(M7C)
                    Dim D As Integer = Sign16(M7D)
                    Dim X As Integer = Sign13(M7X)
                    Dim Y As Integer = Sign13(M7Y)
                    Dim H As Integer = Sign13(M7H)
                    Dim V As Integer = Sign13(M7V)

                    Dim StartX As Integer = 0
                    Dim StartY As Integer = Line + 1
                    If M7Sel And 1 Then StartX = &HFF
                    If M7Sel And 2 Then StartY = StartY Xor &HFF

                    Dim A2 As Integer = (A * Clip10(H - X)) And Not &H3F
                    Dim B2 As Integer = (B * Clip10(V - Y)) And Not &H3F
                    Dim C2 As Integer = (C * Clip10(H - X)) And Not &H3F
                    Dim D2 As Integer = (D * Clip10(V - Y)) And Not &H3F

                    Dim BmpX As Integer = A2 + B2 + A * StartX + ((B * StartY) And Not &H3F) + (X << 8)
                    Dim BmpY As Integer = C2 + D2 + C * StartX + ((D * StartY) And Not &H3F) + (Y << 8)

                    For ScrnX As Integer = StartX To StartX Xor &HFF
                        Dim XOver As Boolean = BmpX And Not &H3FFFF
                        Dim YOver As Boolean = BmpY And Not &H3FFFF

                        Dim TMX As Integer = (BmpX >> 11) And &H7F
                        Dim TMY As Integer = (BmpY >> 11) And &H7F

                        Dim PixelX As Integer = (BmpX >> 8) And 7
                        Dim PixelY As Integer = (BmpY >> 8) And 7

                        If M7Sel And 1 Then
                            BmpX = BmpX - A
                            BmpY = BmpY - C
                        Else
                            BmpX = BmpX + A
                            BmpY = BmpY + C
                        End If

                        Dim ChrAddr As Integer = VRAM((TMX + (TMY << 7)) << 1) * 128

                        If XOver Or YOver Then
                            If ScrnOver = 2 Then
                                Offset = Offset + 4
                                Continue For
                            End If

                            If ScrnOver = 3 Then ChrAddr = 0
                        End If

                        Dim Color As Byte = VRAM(ChrAddr + 1 + (PixelY << 4) + (PixelX << 1))

                        DrawPixel(Layer, Offset, Color)

                        Offset = Offset + 4
                    Next
                Else
                    If BgMode And (&H10 << Layer) Then
                        ' --- 16x16
                        Dim TMBase As Integer = (.SC And &HFC) << 9
                        Dim TMY As Integer = ((Line + .VOfs) And &H1FF) >> 4
                        Dim TMAddr As Integer = TMBase + (TMY << 6)
                        Dim TMSY As Integer = ((Line + .VOfs) >> 9) And 1

                        For TX As Integer = 0 To 16
                            Dim TMSX As Integer = (TX << 4) + .HOfs
                            Dim XAddr As Integer = (TMSX And &H1F0) >> 3
                            Dim TAddr As Integer = TMAddr + XAddr

                            TMSX = (TMSX >> 9) And 1

                            Select Case .SC And 3
                                Case 1 : TAddr = TAddr + (TMSX << 11)
                                Case 2 : TAddr = TAddr + (TMSY << 11)
                                Case 3 : TAddr = TAddr + (TMSX << 11) + (TMSY << 12)
                            End Select

                            TAddr = TAddr And &HFFFE

                            Dim Pri As Boolean = VRAM(TAddr + 1) And &H20

                            If Pri = Fg Then
                                Dim VL As Integer = VRAM(TAddr)
                                Dim VH As Integer = VRAM(TAddr + 1)
                                Dim Tile As Integer = VL Or (VH << 8)
                                Dim ChrNum As Integer = Tile And &H3FF
                                Dim CGNum As Integer = ((Tile And &H1C00) >> 10) << BPP
                                Dim HFlip As Boolean = Tile And &H4000
                                Dim VFlip As Boolean = Tile And &H8000

                                Dim YOfs As Integer = (Line + .VOfs) And 7
                                If VFlip Then YOfs = YOfs Xor 7
                                If VFlip Then
                                    If ((Line + .VOfs) And 8) = 0 Then ChrNum = ChrNum + &H10
                                Else
                                    If (Line + .VOfs) And 8 Then ChrNum = ChrNum + &H10
                                End If

                                Dim ChrAddr As Integer = (.ChrBase + (ChrNum << BPPLSh) + (YOfs << 1)) And &HFFFF

                                For TBX As Integer = 0 To 1
                                    For X As Integer = 0 To 7
                                        Dim XBit As Integer = X
                                        Dim TBXOfs As Integer = TBX << 3

                                        If HFlip Then
                                            XBit = XBit Xor 7
                                            TBXOfs = TBXOfs Xor 8
                                        End If

                                        Dim PalColor As Byte = ReadChr(ChrAddr, BPP, XBit)

                                        If PalColor <> 0 Then
                                            Dim Offset As Integer = ((TX << 4) + X + TBXOfs - (.HOfs And &HF)) << 2
                                            Dim Color As Integer = (CGNum + PalColor) And &HFF
                                            If Offset >= 1024 Then Exit For
                                            If Offset < 0 Then Continue For

                                            DrawPixel(Layer, Offset, Color)
                                        End If
                                    Next

                                    ChrAddr = ChrAddr + (BPP << 3)
                                Next
                            End If
                        Next
                        ' --- 16x16
                    Else
                        ' --- 8x8
                        Dim TMBase As Integer = (.SC And &HFC) << 9
                        Dim TMY As Integer = ((Line + .VOfs) And &HFF) >> 3
                        Dim TMAddr As Integer = TMBase + (TMY << 6)
                        Dim TMSY As Integer = ((Line + .VOfs) >> 8) And 1

                        For TX As Integer = 0 To 32
                            Dim TMSX As Integer = (TX << 3) + .HOfs
                            Dim XAddr As Integer = (TMSX And &HF8) >> 2
                            Dim TAddr As Integer = TMAddr + XAddr

                            TMSX = (TMSX >> 8) And 1

                            Select Case .SC And 3
                                Case 1 : TAddr = TAddr + (TMSX << 11)
                                Case 2 : TAddr = TAddr + (TMSY << 11)
                                Case 3 : TAddr = TAddr + (TMSX << 11) + (TMSY << 12)
                            End Select

                            TAddr = TAddr And &HFFFE

                            Dim Pri As Boolean = VRAM(TAddr + 1) And &H20

                            If Pri = Fg Then
                                Dim VL As Integer = VRAM(TAddr)
                                Dim VH As Integer = VRAM(TAddr + 1)
                                Dim Tile As Integer = VL Or (VH << 8)
                                Dim ChrNum As Integer = Tile And &H3FF
                                Dim CGNum As Integer = ((Tile And &H1C00) >> 10) << BPP
                                Dim HFlip As Boolean = Tile And &H4000
                                Dim VFlip As Boolean = Tile And &H8000

                                Dim YOfs As Integer = (Line + .VOfs) And 7
                                If VFlip Then YOfs = YOfs Xor 7

                                Dim ChrAddr As Integer = (.ChrBase + (ChrNum << BPPLSh) + (YOfs << 1)) And &HFFFF

                                For X As Integer = 0 To 7
                                    Dim XBit As Integer = X
                                    If HFlip Then XBit = XBit Xor 7

                                    Dim PalColor As Byte = ReadChr(ChrAddr, BPP, XBit)

                                    If PalColor <> 0 Then
                                        Dim Offset As Integer = ((TX << 3) + X - (.HOfs And 7)) << 2
                                        Dim Color As Integer = (CGNum + PalColor) And &HFF
                                        If Offset >= 1024 Then Exit For
                                        If Offset < 0 Then Continue For

                                        DrawPixel(Layer, Offset, Color)
                                    End If
                                Next
                            End If
                        Next
                        ' --- 8x8
                    End If
                End If
            End With
        End If
    End Sub

    Private Sub DrawPixel(Layer As Integer, Offset As Integer, Color As Integer)
        If TS And (1 << Layer) Then
            SScrn(Offset + 0) = Pal(Color).B
            SScrn(Offset + 1) = Pal(Color).G
            SScrn(Offset + 2) = Pal(Color).R

            SZOrder(Offset >> 2) = Layer
        End If

        If TM And (1 << Layer) Then
            MScrn(Offset + 0) = Pal(Color).B
            MScrn(Offset + 1) = Pal(Color).G
            MScrn(Offset + 2) = Pal(Color).R

            MZOrder(Offset >> 2) = Layer
        End If
    End Sub

    Private Function ReadChr(Address As Integer, BPP As Integer, X As Integer) As Byte
        Dim Color As Byte = 0
        Dim Bit As Integer = &H80 >> X

        If VRAM(Address + 0) And Bit Then Color = Color Or &H1
        If VRAM(Address + 1) And Bit Then Color = Color Or &H2

        If BPP <> 2 Then
            If VRAM(Address + 16) And Bit Then Color = Color Or &H4
            If VRAM(Address + 17) And Bit Then Color = Color Or &H8

            If BPP = 8 Then
                If VRAM(Address + 32) And Bit Then Color = Color Or &H10
                If VRAM(Address + 33) And Bit Then Color = Color Or &H20
                If VRAM(Address + 48) And Bit Then Color = Color Or &H40
                If VRAM(Address + 49) And Bit Then Color = Color Or &H80
            End If
        End If

        ReadChr = Color
    End Function

    Private Function Clip10(Value As Integer) As Integer
        If Value And &H2000 Then Clip10 = Value Or Not &H3FF Else Clip10 = Value And &H3FF
    End Function
End Class
