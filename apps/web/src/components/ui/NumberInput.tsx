import { useState, useEffect, useRef } from 'react';

interface NumberInputProps {
  value: number | undefined;
  onChange: (value: number | undefined) => void;
  placeholder?: string;
  min?: number;
  max?: number;
  step?: number | 'any';
  required?: boolean;
  className?: string;
  prefix?: string;
  suffix?: string;
  disabled?: boolean;
  allowEmpty?: boolean;
  defaultValue?: number;
}

export function NumberInput({
  value,
  onChange,
  placeholder,
  min,
  max,
  step = 1,
  required = false,
  className = '',
  prefix,
  suffix,
  disabled = false,
  allowEmpty = false,
  defaultValue
}: NumberInputProps) {
  // Use string state to handle intermediate input states (like typing decimals)
  const [displayValue, setDisplayValue] = useState<string>('');
  const [isFocused, setIsFocused] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Update display value when prop value changes (but not when focused to avoid interrupting user input)
  useEffect(() => {
    if (!isFocused) {
      if (value !== undefined && value !== null) {
        setDisplayValue(value.toString());
      } else if (defaultValue !== undefined && (value === undefined || value === null)) {
        setDisplayValue(defaultValue.toString());
      } else {
        setDisplayValue('');
      }
    }
  }, [value, isFocused, defaultValue]);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const inputValue = e.target.value;
    setDisplayValue(inputValue);

    // Handle empty input
    if (inputValue === '' || inputValue === '-') {
      if (allowEmpty) {
        onChange(undefined);
      } else if (defaultValue !== undefined) {
        onChange(defaultValue);
      } else {
        onChange(undefined);
      }
      return;
    }

    // Parse the number
    const numericValue = parseFloat(inputValue);
    
    // Only update if it's a valid number
    if (!isNaN(numericValue)) {
      // Apply min/max constraints
      let constrainedValue = numericValue;
      if (min !== undefined && constrainedValue < min) {
        constrainedValue = min;
      }
      if (max !== undefined && constrainedValue > max) {
        constrainedValue = max;
      }
      
      onChange(constrainedValue);
    }
  };

  const handleFocus = () => {
    setIsFocused(true);
  };

  const handleBlur = () => {
    setIsFocused(false);
    
    // On blur, ensure we have a valid value or handle empty state
    if (displayValue === '' || displayValue === '-') {
      if (!allowEmpty) {
        if (defaultValue !== undefined) {
          setDisplayValue(defaultValue.toString());
          onChange(defaultValue);
        } else if (min !== undefined) {
          setDisplayValue(min.toString());
          onChange(min);
        } else {
          setDisplayValue('0');
          onChange(0);
        }
      }
    } else {
      // Ensure display value matches the actual numeric value
      const numericValue = parseFloat(displayValue);
      if (!isNaN(numericValue)) {
        setDisplayValue(numericValue.toString());
      }
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    // Allow: backspace, delete, tab, escape, enter
    if ([46, 8, 9, 27, 13].includes(e.keyCode) ||
        // Allow: Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
        (e.keyCode === 65 && e.ctrlKey) ||
        (e.keyCode === 67 && e.ctrlKey) ||
        (e.keyCode === 86 && e.ctrlKey) ||
        (e.keyCode === 88 && e.ctrlKey) ||
        // Allow: home, end, left, right
        (e.keyCode >= 35 && e.keyCode <= 39)) {
      return;
    }
    
    // Allow: minus sign at the start (if min is not set or allows negative)
    if (e.key === '-' && (min === undefined || min < 0) && inputRef.current?.selectionStart === 0) {
      return;
    }
    
    // Allow: decimal point (only one)
    if (e.key === '.' && step !== 1 && !displayValue.includes('.')) {
      return;
    }
    
    // Ensure that it is a number and stop the keypress
    if ((e.shiftKey || (e.keyCode < 48 || e.keyCode > 57)) && (e.keyCode < 96 || e.keyCode > 105)) {
      e.preventDefault();
    }
  };

  const baseInputClasses = "block w-full rounded-lg border border-input bg-card text-foreground text-xs tabular-nums px-3 py-2 focus:outline-none focus:border-ring transition-colors disabled:opacity-50 disabled:cursor-not-allowed";
  const inputClasses = `${baseInputClasses} ${className}`;

  if (prefix || suffix) {
    return (
      <div className="relative">
        {prefix && (
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <span className="text-muted-foreground text-xs">{prefix}</span>
          </div>
        )}
        <input
          ref={inputRef}
          type="text"
          inputMode="decimal"
          value={displayValue}
          onChange={handleInputChange}
          onFocus={handleFocus}
          onBlur={handleBlur}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          required={required}
          disabled={disabled}
          className={`${inputClasses} ${prefix ? 'pl-7' : ''} ${suffix ? 'pr-12' : ''}`}
        />
        {suffix && (
          <div className="absolute inset-y-0 right-0 pr-3 flex items-center pointer-events-none">
            <span className="text-muted-foreground text-xs">{suffix}</span>
          </div>
        )}
      </div>
    );
  }

  return (
    <input
      ref={inputRef}
      type="text"
      inputMode="decimal"
      value={displayValue}
      onChange={handleInputChange}
      onFocus={handleFocus}
      onBlur={handleBlur}
      onKeyDown={handleKeyDown}
      placeholder={placeholder}
      required={required}
      disabled={disabled}
      className={inputClasses}
    />
  );
}