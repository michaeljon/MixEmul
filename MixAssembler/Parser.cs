using System.Collections.Generic;
using MixAssembler.Finding;
using MixAssembler.Instruction;
using MixAssembler.Symbol;
using MixAssembler.Value;
using MixLib;
using MixLib.Instruction;
using MixLib.Type;

namespace MixAssembler
{
	/// <summary>
	/// This class contains the logic for parsing MIXAL code. 
	/// </summary>
	public static class Parser
	{
		public const int FieldSpacing = 2;
		public const int MinAddressLength = 20;
		public const int MinLocLength = 10;
		public const int MinOpLength = 4;

        const int locFieldIndex = 0;
        const int opFieldIndex = 1;
        const int addressFieldIndex = 2;
        const int commentFieldIndex = 3;

        static InstructionSet mInstructionSet = new InstructionSet();
        static LoaderInstructions mLoaderInstructions = new LoaderInstructions();

        static bool IsCommentLine(string sourceLine) => sourceLine.Trim().Length == 0 || sourceLine[0] == '*';

        /// <summary>
        /// This method parses an "in-memory" instruction. That is: an instruction without a location field. 
        /// </summary>
        public static ParsedSourceLine ParseInstructionLine(string instructionLine, ParsingStatus status) =>
            // add an empty location field, then parse as usual.
            ParseLine(" " + instructionLine, status);

        static int FindFirstNonWhiteSpace(string sourceLine, int searchBeyondIndex)
        {
            for (int i = searchBeyondIndex + 1; i < sourceLine.Length; i++)
            {
                if (!char.IsWhiteSpace(sourceLine, i)) return i;
            }

            return -1;
        }

        static int FindFirstWhiteSpace(string sourceLine, int searchBeyondIndex)
        {
            for (int i = searchBeyondIndex + 1; i < sourceLine.Length; i++)
            {
                if (char.IsWhiteSpace(sourceLine, i)) return i;
            }

            return -1;
        }

        /// <summary>
        /// This method is used to handle instructions targeted at the assembler itself. 
        /// </summary>
        static bool HandleAssemblyInstruction(string opField, string addressField, SymbolBase symbol, ParsingStatus status)
        {
            IValue expression;
            status.LineSection = LineSection.AddressField;
            switch (opField)
            {
                case "EQU":
                    // set value of the instruction symbol (first field) to the value of the expression that is in the address field
                    if (symbol != null)
                    {
                        expression = WValue.ParseValue(addressField, 0, status);

                        if (!expression.IsValueDefined(status.LocationCounter))
                        {
                            status.ReportParsingError(0, addressField.Length, "expression value is undefined");
                            return true;
                        }

                        symbol.SetValue(expression.GetSign(status.LocationCounter), expression.GetMagnitude(status.LocationCounter));
                    }

                    return true;

                case "ORIG":
                    // set the location counter to the value of the expression that is in the address field
                    expression = WValue.ParseValue(addressField, 0, status);

                    if (!expression.IsValueDefined(status.LocationCounter))
                    {
                        status.ReportParsingError(0, addressField.Length, "expression value is undefined");
                        return true;
                    }

                    status.LocationCounter = (int)expression.GetValue(status.LocationCounter);

                    return true;

                case "CON":
                case "ALF":
                    // these instructions set the value of the memory word at the location counter, which is actually a loader instruction. 
                    // However, during assembly these memory words must be skipped, which is why we increase the location counter.
                    status.LocationCounter++;

                    return true;
            }

            return false;
        }

        static void GetMixOrLoaderInstructionAndParameters(string opField, string addressField, ParsingStatus status, out InstructionBase instruction, out IInstructionParameters instructionParameters)
        {
            status.LineSection = LineSection.AddressField;
            instructionParameters = null;

            instruction = mLoaderInstructions[opField];

            if (instruction != null)
            {
                instructionParameters = LoaderInstructionParameters.ParseAddressField(instruction, addressField, status);
            }
            else
            {
                instruction = mInstructionSet[opField];

                if (instruction != null)
                {
                    instructionParameters = MixInstructionParameters.ParseAddressField(instruction, addressField, status);
                    status.LocationCounter++;
                }
            }
        }

        static ParsedSourceLine ParseLine(string sourceLine, ParsingStatus status)
        {

            if (IsCommentLine(sourceLine)) return new ParsedSourceLine(status.LineNumber, sourceLine);

            var lineFields = SplitLine(sourceLine);
            lineFields[locFieldIndex] = lineFields[locFieldIndex].ToUpper();
            lineFields[opFieldIndex] = lineFields[opFieldIndex].ToUpper();
            lineFields[addressFieldIndex] = lineFields[addressFieldIndex].ToUpper();

            if (lineFields[opFieldIndex] == "")
            {
                status.ReportParsingError(LineSection.LocationField, 0, sourceLine.Length, "op and address fields are missing");

                return new ParsedSourceLine(status.LineNumber, lineFields[0], "", "", "", null, null);
            }

            var symbol = ParseLocField(lineFields[locFieldIndex], status);
            // if the location field contains a symbol name, set its value to the location counter
            symbol?.SetValue(status.LocationCounter);

            GetMixOrLoaderInstructionAndParameters(lineFields[opFieldIndex], lineFields[addressFieldIndex], status, out InstructionBase instruction, out IInstructionParameters instructionParameters);

            // the following call must be made even if a MIX or loader instruction was found, as some loader instructions require the assembler to act, as well
            var assemblyInstructionHandled = HandleAssemblyInstruction(lineFields[opFieldIndex], lineFields[addressFieldIndex], symbol, status);

            // if the line isn't a comment or a MIX or loader instruction, it must be an assembler instruction. If not, we don't know the mnemonic
            if (instruction == null && !assemblyInstructionHandled)
            {
                status.ReportParsingError(LineSection.OpField, 0, lineFields[opFieldIndex].Length, "operation mnemonic unknown");
            }

            return new ParsedSourceLine(status.LineNumber, lineFields[locFieldIndex], lineFields[opFieldIndex], lineFields[addressFieldIndex], lineFields[commentFieldIndex], instruction, instructionParameters);
        }

        static SymbolBase ParseLocField(string locField, ParsingStatus status)
        {
            if (locField == "") return null;

            status.LineSection = LineSection.LocationField;

            var symbol = ValueSymbol.ParseDefinition(locField, 0, status);
            if (symbol == null)
            {
                status.ReportParsingError(0, locField.Length, "invalid symbol name");
                return null;
            }

            if (status.Symbols.Contains(symbol))
            {
                symbol = status.Symbols[symbol.Name];

                if (symbol.IsSymbolDefined && !symbol.IsMultiValuedSymbol)
                {
                    status.ReportParsingError(0, locField.Length, "symbol already defined");
                    return null;
                }

                return symbol;
            }

            status.Symbols.Add(symbol);
            return symbol;
        }

        public static PreInstruction[] ParseSource(string[] sourceLines, ParsingStatus status)
		{
			status.LocationCounter = 0;
			int endLineNumber = -1;
			var preInstructions = new List<PreInstruction>();

			for (int lineNumber = 0; lineNumber < sourceLines.Length; lineNumber++)
			{
				status.LineNumber = lineNumber;

				if (endLineNumber != -1 && !IsCommentLine(sourceLines[lineNumber]))
				{
					status.ReportParsingWarning(LineSection.CommentField, 0, sourceLines[lineNumber].Length, "END has been parsed; line will be treated as comment");
					preInstructions.Add(new ParsedSourceLine(lineNumber, sourceLines[lineNumber]));
				}
				else
				{
					var parsedLine = ParseLine(sourceLines[lineNumber], status);
					preInstructions.Add(parsedLine);
					if (parsedLine.OpField == "END")
					{
						endLineNumber = preInstructions.Count - 1;
					}
				}
			}

			if (endLineNumber == -1)
			{
				status.Findings.Add(new AssemblyError(int.MinValue, LineSection.CommentField, 0, 0, new ParsingError("END operation is mandatory but not included")));
			}
			else
			{
				// all symbols that are not yet defined at the end of the parsing process are treated as if "<symbolname> CON *" instructions were included just before the END instruction
				foreach (SymbolBase symbol in status.Symbols)
				{
					if (!symbol.IsSymbolDefined)
					{
						symbol.SetValue(status.LocationCounter);

						var parameters = new LoaderInstructionParameters(new NumberValue(symbol.MemoryWordSign, symbol.MemoryWordMagnitude), 0);
						var instruction = new PreInstruction(mLoaderInstructions["CON"], parameters);

						preInstructions.Insert(endLineNumber, instruction);
						status.LocationCounter++;
						endLineNumber++;
					}
				}
			}

			return preInstructions.ToArray();
		}

        static string[] SplitLine(string sourceLine)
        {
            int addressFieldEnd;
            var searchBeyondIndex = FindFirstWhiteSpace(sourceLine, -1);
            if (searchBeyondIndex == -1) return new string[] { sourceLine, "", "", "" };

            var opFieldStart = FindFirstNonWhiteSpace(sourceLine, searchBeyondIndex);
            if (opFieldStart == -1) return new string[] { sourceLine.Substring(0, searchBeyondIndex), "", "", "" };

            var opFieldEnd = FindFirstWhiteSpace(sourceLine, opFieldStart);
            if (opFieldEnd == -1)
            {
                return new string[] { sourceLine.Substring(0, searchBeyondIndex), sourceLine.Substring(opFieldStart), "", "" };
            }

            int opFieldLength = opFieldEnd - opFieldStart;
            var addressFieldStart = FindFirstNonWhiteSpace(sourceLine, opFieldEnd);
            if (addressFieldStart == -1)
            {
                return new string[] { sourceLine.Substring(0, searchBeyondIndex), sourceLine.Substring(opFieldStart, opFieldLength), "", "" };
            }

            if (sourceLine[addressFieldStart] == '"')
            {
                addressFieldEnd = sourceLine.LastIndexOf('"');
                if (addressFieldEnd == addressFieldStart || addressFieldEnd == sourceLine.Length - 1 || !char.IsWhiteSpace(sourceLine, addressFieldEnd + 1))
                {
                    addressFieldEnd = -1;
                }
                else
                {
                    addressFieldEnd++;
                }
            }
            else
            {
                addressFieldEnd = FindFirstWhiteSpace(sourceLine, addressFieldStart);
            }

            if (addressFieldEnd == -1)
            {
                return new string[] { sourceLine.Substring(0, searchBeyondIndex), sourceLine.Substring(opFieldStart, opFieldLength), sourceLine.Substring(addressFieldStart), "" };
            }

            int addressFieldLength = addressFieldEnd - addressFieldStart;
            var commentFieldStart = FindFirstNonWhiteSpace(sourceLine, addressFieldEnd);
            if (commentFieldStart == -1)
            {
                return new string[] { sourceLine.Substring(0, searchBeyondIndex), sourceLine.Substring(opFieldStart, opFieldLength), sourceLine.Substring(addressFieldStart, addressFieldLength), "" };
            }

            return new string[] { sourceLine.Substring(0, searchBeyondIndex), sourceLine.Substring(opFieldStart, opFieldLength), sourceLine.Substring(addressFieldStart, addressFieldLength), sourceLine.Substring(commentFieldStart) };
        }
    }
}
